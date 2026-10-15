# Recovery gate retired in ServoERP 1.1.515

The former journal-based startup gate, forced upgrade backup, original-release retry command, and recovery dialog are removed. Existing migration records and backups are preserved. SQL Server remains authoritative. Ordinary schema guards, required financial schema, and normal business audit/protection remain. Schema maintenance does not replay financial audit history. A real SQL/schema failure still stops initialization and is reported normally.
