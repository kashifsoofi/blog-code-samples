namespace Movies.Api.Store;

public class RecordNotFoundException : Exception
{
    public RecordNotFoundException() : base() { }

    public RecordNotFoundException(string message)
        : base(message)
    { }

    public RecordNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    { }
}

