namespace pr3withDB.Common
{
    public enum ServiceErrorType
    {
        None,
        NotFound,
        Validation,
        Conflict,
        Unauthorized,
        Forbidden,
        Internal
    }

    public class ServiceResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public string? ErrorMessage { get; private set; }
        public ServiceErrorType ErrorType { get; private set; }

        public static ServiceResult<T> Success(T data) => new()
        {
            IsSuccess = true,
            Data = data,
            ErrorType = ServiceErrorType.None
        };

        public static ServiceResult<T> Failed(string message, ServiceErrorType errorType = ServiceErrorType.Validation) => new()
        {
            IsSuccess = false,
            ErrorMessage = message,
            ErrorType = errorType
        };

        public static ServiceResult<T> NotFound(string message = "Запрашиваемый ресурс не найден") => new()
        {
            IsSuccess = false,
            ErrorMessage = message,
            ErrorType = ServiceErrorType.NotFound
        };

        public static ServiceResult<T> Unauthorized(string message = "Неверные учетные данные") => new()
        {
            IsSuccess = false,
            ErrorMessage = message,
            ErrorType = ServiceErrorType.Unauthorized
        };

        public static ServiceResult<T> Forbidden(string message = "Доступ запрещен") => new()
        {
            IsSuccess = false,
            ErrorMessage = message,
            ErrorType = ServiceErrorType.Forbidden
        };
    }
}
