namespace MusicPlayer.Core.Playback;

/// <summary>
/// 随机播放用的「抽签袋」：把整张列表打乱后逐个取出，取完再重新打乱。
/// 这样每一轮里每首歌都放过一次才会出现重复；纯随机做不到这点
/// （可能连着放同一首，也可能放了几首就一直在小圈子里打转）。
/// </summary>
public sealed class ShuffleBag
{
    private readonly Func<int, int> _pickIndex;
    private readonly List<int> _remaining = new();
    private readonly List<int> _history = new();

    public ShuffleBag(Func<int, int>? pickIndex = null)
        => _pickIndex = pickIndex ?? (maxExclusive => Random.Shared.Next(maxExclusive));

    /// <summary>本轮还没放过的曲目数。</summary>
    public int RemainingCount => _remaining.Count;

    /// <summary>取下一首。袋子空了就重新装一轮，并保证新一轮的第一首不是刚放完的那首。</summary>
    public int Next(int count, int current)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (count == 1)
        {
            return 0;
        }

        if (_remaining.Count == 0)
        {
            Refill(count);

            // 新一轮的第一首不能正好是刚放完的那首，否则听起来像卡住了
            if (_remaining[0] == current)
            {
                _remaining.RemoveAt(0);
                _remaining.Add(current);
            }
        }

        var next = _remaining[0];
        _remaining.RemoveAt(0);

        if (current >= 0)
        {
            _history.Add(current);
        }

        return next;
    }

    /// <summary>回到上一首；没有历史可退时返回 current（调用方据此判断"没有上一首"）。</summary>
    public int Previous(int current)
    {
        if (_history.Count == 0)
        {
            return current;
        }

        var previous = _history[^1];
        _history.RemoveAt(_history.Count - 1);

        // 当前这首退回本轮，等剩下的放完再轮到它
        if (current >= 0 && !_remaining.Contains(current))
        {
            _remaining.Add(current);
        }

        return previous;
    }

    /// <summary>用户在列表里直接点了某一首：它算本轮已播，本轮不会再出现。</summary>
    public void MarkPlayed(int index, int count)
    {
        if (index < 0 || count <= 0)
        {
            return;
        }

        if (_remaining.Count == 0)
        {
            Refill(count);
        }

        _remaining.Remove(index);
    }

    public void Clear()
    {
        _remaining.Clear();
        _history.Clear();
    }

    private void Refill(int count)
    {
        _remaining.Clear();
        for (var i = 0; i < count; i++)
        {
            _remaining.Add(i);
        }

        // Fisher-Yates 洗牌。随机源可注入，测试里能固定结果。
        for (var i = _remaining.Count - 1; i > 0; i--)
        {
            var j = _pickIndex(i + 1);
            (_remaining[i], _remaining[j]) = (_remaining[j], _remaining[i]);
        }
    }
}
