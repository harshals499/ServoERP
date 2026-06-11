using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ServoERP.Infrastructure;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>
    /// Bridges the WinForms Dev Team Dashboard to the local-first ServoERP Brain
    /// (Python + Ollama + SQLite agent orchestrator under ServoERPBrain/).
    /// </summary>
    public sealed class DevTeamBrainService
    {
        private static readonly DevTeamBrainService _instance = new DevTeamBrainService();

        private static readonly string BrainRoot = Path.Combine(@"C:\HVAC_PRO_MSE", "ServoERPBrain");
        private static readonly string DbPath = Path.Combine(BrainRoot, "memory", "servoerp_brain.db");
        private static readonly string OrchestratorDir = Path.Combine(BrainRoot, "orchestrator");
        private static readonly string SupervisorScript = Path.Combine(OrchestratorDir, "supervisor.py");
        private static readonly string AgentLogPath = Path.Combine(BrainRoot, "logs", "agent_actions.log");

        private readonly object _sync = new object();
        private System.Windows.Forms.Timer _timer;

        public event EventHandler<DevTeamBrainProgressEventArgs> ProgressChanged;

        public static DevTeamBrainService Instance
        {
            get { return _instance; }
        }

        private DevTeamBrainService()
        {
        }

        public bool DatabaseExists
        {
            get { return File.Exists(DbPath); }
        }

        public void StartPolling(int intervalMs = 2000)
        {
            lock (_sync)
            {
                if (_timer != null)
                    return;

                _timer = new System.Windows.Forms.Timer { Interval = intervalMs };
                _timer.Tick += (s, e) => Poll();
                _timer.Start();
            }

            Poll();
        }

        public void StopPolling()
        {
            lock (_sync)
            {
                if (_timer == null)
                    return;

                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        private void Poll()
        {
            try
            {
                DevTeamBrainSnapshot snapshot = GetSnapshot();
                ProgressChanged?.Invoke(this, new DevTeamBrainProgressEventArgs(snapshot));
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamBrainService.Poll");
            }
        }

        // ---------------------------------------------------------------
        // Reads
        // ---------------------------------------------------------------

        public DevTeamBrainSnapshot GetSnapshot()
        {
            var snapshot = new DevTeamBrainSnapshot();
            if (!DatabaseExists)
                return snapshot;

            using (SQLiteConnection conn = OpenConnection())
            {
                using (var cmd = new SQLiteCommand(
                    "SELECT id, name, role, status, current_task_id, last_action, updated_at " +
                    "FROM agents ORDER BY id", conn))
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        snapshot.Agents.Add(new DevTeamAgentStatus
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Role = reader.GetString(2),
                            Status = reader.GetString(3),
                            CurrentTaskId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                            LastAction = reader.IsDBNull(5) ? null : reader.GetString(5),
                            UpdatedAt = ParseDateTime(reader.GetString(6)),
                        });
                    }
                }

                using (var cmd = new SQLiteCommand(
                    "SELECT id, description, status, requires_human_review, build_status, " +
                    "test_status, stop_requested, report_path, created_at, updated_at " +
                    "FROM tasks ORDER BY id DESC LIMIT 50", conn))
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        snapshot.Tasks.Add(ReadTask(reader));
                    }
                }
            }

            return snapshot;
        }

        public DevTeamTaskDetail GetTaskDetail(int taskId)
        {
            var detail = new DevTeamTaskDetail();
            if (!DatabaseExists)
                return detail;

            using (SQLiteConnection conn = OpenConnection())
            {
                using (var cmd = new SQLiteCommand(
                    "SELECT id, description, status, requires_human_review, build_status, " +
                    "test_status, stop_requested, report_path, created_at, updated_at " +
                    "FROM tasks WHERE id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", taskId);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            detail.Task = ReadTask(reader);
                    }
                }

                using (var cmd = new SQLiteCommand(
                    "SELECT id, task_id, agent_name, step_order, action, status, started_at, " +
                    "finished_at, summary FROM task_steps WHERE task_id = @id ORDER BY step_order", conn))
                {
                    cmd.Parameters.AddWithValue("@id", taskId);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            detail.Steps.Add(new DevTeamTaskStep
                            {
                                Id = reader.GetInt32(0),
                                TaskId = reader.GetInt32(1),
                                AgentName = reader.GetString(2),
                                StepOrder = reader.GetInt32(3),
                                Action = reader.GetString(4),
                                Status = reader.GetString(5),
                                StartedAt = reader.IsDBNull(6) ? (DateTime?)null : ParseDateTime(reader.GetString(6)),
                                FinishedAt = reader.IsDBNull(7) ? (DateTime?)null : ParseDateTime(reader.GetString(7)),
                                Summary = reader.IsDBNull(8) ? null : reader.GetString(8),
                            });
                        }
                    }
                }

                using (var cmd = new SQLiteCommand(
                    "SELECT id, task_id, agent_name, level, message, created_at FROM agent_logs " +
                    "WHERE task_id = @id ORDER BY id DESC LIMIT 200", conn))
                {
                    cmd.Parameters.AddWithValue("@id", taskId);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            detail.Logs.Add(new DevTeamLogEntry
                            {
                                Id = reader.GetInt32(0),
                                TaskId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                                AgentName = reader.GetString(2),
                                Level = reader.GetString(3),
                                Message = reader.GetString(4),
                                CreatedAt = ParseDateTime(reader.GetString(5)),
                            });
                        }
                    }
                }

                using (var cmd = new SQLiteCommand(
                    "SELECT id, task_id, file_path, change_type, staged_path, applied " +
                    "FROM file_changes WHERE task_id = @id ORDER BY id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", taskId);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            detail.FileChanges.Add(new DevTeamFileChange
                            {
                                Id = reader.GetInt32(0),
                                TaskId = reader.GetInt32(1),
                                FilePath = reader.GetString(2),
                                ChangeType = reader.GetString(3),
                                StagedPath = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Applied = reader.GetInt64(5) != 0,
                            });
                        }
                    }
                }

                using (var cmd = new SQLiteCommand(
                    "SELECT id, task_id, kind, passed, output_summary, created_at " +
                    "FROM test_results WHERE task_id = @id ORDER BY id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", taskId);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            detail.TestResults.Add(new DevTeamTestResult
                            {
                                Id = reader.GetInt32(0),
                                TaskId = reader.GetInt32(1),
                                Kind = reader.GetString(2),
                                Passed = reader.GetInt64(3) != 0,
                                OutputSummary = reader.IsDBNull(4) ? null : reader.GetString(4),
                                CreatedAt = ParseDateTime(reader.GetString(5)),
                            });
                        }
                    }
                }
            }

            return detail;
        }

        public string GetReportFullPath(string relativeReportPath)
        {
            if (string.IsNullOrWhiteSpace(relativeReportPath))
                return null;

            return Path.Combine(@"C:\HVAC_PRO_MSE", relativeReportPath.Replace('/', '\\'));
        }

        // ---------------------------------------------------------------
        // Writes / actions
        // ---------------------------------------------------------------

        /// <summary>Creates a new task row and launches the brain pipeline for it.</summary>
        public int SubmitTask(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Task description is required", nameof(description));

            if (!DatabaseExists)
                throw new InvalidOperationException(
                    "ServoERP Brain database not found. Run 'python ServoERPBrain/memory/init_db.py' once to initialize it.");

            int taskId;
            using (SQLiteConnection conn = OpenConnection())
            {
                string nowIso = NowIso();
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO tasks (description, status, created_at, updated_at) " +
                    "VALUES (@desc, 'pending', @now, @now); SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@desc", description);
                    cmd.Parameters.AddWithValue("@now", nowIso);
                    taskId = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
            }

            StartSupervisorProcess(taskId);
            return taskId;
        }

        /// <summary>Clones an existing task's description into a new task and re-runs it.</summary>
        public int RetryTask(int taskId)
        {
            if (!DatabaseExists)
                throw new InvalidOperationException("ServoERP Brain database not found.");

            string description;
            using (SQLiteConnection conn = OpenConnection())
            {
                using (var cmd = new SQLiteCommand("SELECT description FROM tasks WHERE id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", taskId);
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                        throw new InvalidOperationException($"Task {taskId} not found.");
                    description = (string)result;
                }
            }

            return SubmitTask(description);
        }

        /// <summary>Requests cooperative stop of a running task. The Python supervisor
        /// checks this flag between pipeline steps.</summary>
        public void StopTask(int taskId)
        {
            if (!DatabaseExists)
                return;

            using (SQLiteConnection conn = OpenConnection())
            {
                using (var cmd = new SQLiteCommand(
                    "UPDATE tasks SET stop_requested = 1, updated_at = @now WHERE id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", taskId);
                    cmd.Parameters.AddWithValue("@now", NowIso());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void StartSupervisorProcess(int taskId)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AgentLogPath) ?? Path.Combine(BrainRoot, "logs"));

                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{SupervisorScript}\" --task-id {taskId}",
                    WorkingDirectory = OrchestratorDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                process.OutputDataReceived += (s, e) => AppendProcessLog(taskId, "stdout", e.Data);
                process.ErrorDataReceived += (s, e) => AppendProcessLog(taskId, "stderr", e.Data);
                process.Exited += (s, e) => process.Dispose();

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Win32Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamBrainService.StartSupervisorProcess");
                RecordStartupFailure(taskId,
                    "Could not launch the local Python brain ('python' not found on PATH). " +
                    "Install Python 3 and ensure it is on PATH, then retry this task. Detail: " + ex.Message);
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamBrainService.StartSupervisorProcess");
                RecordStartupFailure(taskId, "Failed to start ServoERP Brain pipeline: " + ex.Message);
            }
        }

        private void RecordStartupFailure(int taskId, string message)
        {
            try
            {
                using (SQLiteConnection conn = OpenConnection())
                {
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO agent_logs (task_id, agent_name, level, message, created_at) " +
                        "VALUES (@taskId, 'supervisor', 'error', @message, @now)", conn))
                    {
                        cmd.Parameters.AddWithValue("@taskId", taskId);
                        cmd.Parameters.AddWithValue("@message", message);
                        cmd.Parameters.AddWithValue("@now", NowIso());
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new SQLiteCommand(
                        "UPDATE tasks SET status = 'blocked', updated_at = @now WHERE id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", taskId);
                        cmd.Parameters.AddWithValue("@now", NowIso());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamBrainService.RecordStartupFailure");
            }
        }

        private void AppendProcessLog(int taskId, string stream, string line)
        {
            if (line == null)
                return;

            try
            {
                File.AppendAllText(AgentLogPath,
                    $"[{NowIso()}] [task {taskId}] [process:{stream}] {line}{Environment.NewLine}");
            }
            catch
            {
                // best-effort logging only
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static DevTeamTask ReadTask(SQLiteDataReader reader)
        {
            return new DevTeamTask
            {
                Id = reader.GetInt32(0),
                Description = reader.GetString(1),
                Status = reader.GetString(2),
                RequiresHumanReview = reader.GetInt64(3) != 0,
                BuildStatus = reader.GetString(4),
                TestStatus = reader.GetString(5),
                StopRequested = reader.GetInt64(6) != 0,
                ReportPath = reader.IsDBNull(7) ? null : reader.GetString(7),
                CreatedAt = ParseDateTime(reader.GetString(8)),
                UpdatedAt = ParseDateTime(reader.GetString(9)),
            };
        }

        private static DateTime ParseDateTime(string value)
        {
            DateTime result;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
                return result;
            return DateTime.MinValue;
        }

        private static string NowIso()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        private static SQLiteConnection OpenConnection()
        {
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = DbPath,
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Normal,
            };

            var conn = new SQLiteConnection(builder.ConnectionString);
            conn.Open();
            return conn;
        }
    }
}
