namespace MpParserAPI.Common
{
    public class OperationResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T? Data { get; set; }

        public static OperationResult<T> Ok(T data, string message = "Success") =>
            new OperationResult<T> { Success = true, Message = message, Data = data };

        public static OperationResult<T> Ok(string message = "Success") =>
            new OperationResult<T> { Success = true, Message = message };

        public static OperationResult<T> Fail(string message) =>
            new OperationResult<T> { Success = false, Message = message };

        public static OperationResult<T> Fail(T data, string message) =>
            new OperationResult<T> { Success = false, Message = message, Data = data };
    }

}
