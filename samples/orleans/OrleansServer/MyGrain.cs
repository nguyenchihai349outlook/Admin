public interface IMyGrain : IGrainWithGuidKey
{
    Task<int> Ping();
}

public class MyGrain : IMyGrain
{
    private int _counter;

    public Task<int> Ping()
    {
        return Task.FromResult(++_counter);
    }
}
