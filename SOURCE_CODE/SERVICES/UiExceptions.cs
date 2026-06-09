using System;

namespace HVAC_Pro_Desktop.Services
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    public class DatabaseException : Exception
    {
        public DatabaseException(string message, Exception inner) : base(message, inner) { }
    }

    public class NavigationException : Exception
    {
        public NavigationException(string message) : base(message) { }
    }

    public class FileProcessingException : Exception
    {
        public FileProcessingException(string message, Exception inner) : base(message, inner) { }
    }
}
