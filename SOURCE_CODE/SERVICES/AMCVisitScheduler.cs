using System;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Creates the default service visit schedule for an AMC contract.</summary>
    public static class AMCVisitScheduler
    {
        /// <summary>Generates evenly spaced scheduled AMC visits when none exist for the contract.</summary>
        public static void GenerateVisitSchedule(int amcId, DateTime startDate, DateTime endDate, int visitsPerYear)
        {
            if (amcId <= 0 || visitsPerYear <= 0 || endDate.Date <= startDate.Date)
                return;

            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "AMCVisitScheduler.GenerateVisitSchedule");

                using (SqlCommand exists = new SqlCommand("SELECT COUNT(1) FROM AMCVisits WHERE AMCID = @AMCID;", connection))
                {
                    exists.Parameters.AddWithValue("@AMCID", amcId);
                    int existing = Convert.ToInt32(exists.ExecuteScalar());
                    if (existing > 0)
                        return;
                }

                TimeSpan duration = endDate.Date - startDate.Date;
                using (SqlCommand insert = new SqlCommand(@"
INSERT INTO AMCVisits
    (AMCID, VisitNumber, ScheduledDate, Status, CreatedAt, UpdatedAt)
VALUES
    (@AMCID, @VisitNumber, @ScheduledDate, 'Scheduled', GETDATE(), GETDATE());", connection))
                {
                    insert.Parameters.Add("@AMCID", System.Data.SqlDbType.Int);
                    insert.Parameters.Add("@VisitNumber", System.Data.SqlDbType.Int);
                    insert.Parameters.Add("@ScheduledDate", System.Data.SqlDbType.Date);

                    for (int i = 1; i <= visitsPerYear; i++)
                    {
                        double ratio = ((double)i - 0.5d) / visitsPerYear;
                        DateTime scheduled = startDate.Date.AddDays(Math.Max(1, Math.Round(duration.TotalDays * ratio)));
                        if (scheduled > endDate.Date)
                            scheduled = endDate.Date;

                        insert.Parameters["@AMCID"].Value = amcId;
                        insert.Parameters["@VisitNumber"].Value = i;
                        insert.Parameters["@ScheduledDate"].Value = scheduled.Date;
                        insert.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
