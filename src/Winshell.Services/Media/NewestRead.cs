namespace Winshell.Media;

internal sealed class NewestRead
{
    private int _started;
    private int _ended;

    public bool Running => _ended != _started;

    public int Start()
    {
        return Interlocked.Increment(ref _started);
    }

    public bool End(int read)
    {
        if (read != _started)
            return false;
        _ended = read;
        return true;
    }
}
