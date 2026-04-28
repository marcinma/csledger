public sealed class AccountNode
{
    private readonly Dictionary<string, AccountNode> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, decimal> _amounts = new(StringComparer.OrdinalIgnoreCase);

    public AccountNode(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public IReadOnlyDictionary<string, AccountNode> Children => _children;

    public AccountNode GetOrCreateChild(string name)
    {
        if (!_children.TryGetValue(name, out var child))
        {
            child = new AccountNode(name);
            _children[name] = child;
        }

        return child;
    }

    public AccountNode? GetChild(string name)
    {
        return _children.GetValueOrDefault(name);
    }

    public void AddAmount(Money money)
    {
        if (!_amounts.ContainsKey(money.Currency))
            _amounts[money.Currency] = 0;

        _amounts[money.Currency] += money.Amount;
    }

    public void SetAmount(Money money)
    {
        _amounts[money.Currency] = money.Amount;
    }

    public IReadOnlyDictionary<string, decimal> Sum()
    {
        return new Dictionary<string, decimal>(_amounts, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, decimal> TotalRaw()
    {
        var result = new Dictionary<string, decimal>(_amounts, StringComparer.OrdinalIgnoreCase);

        foreach (var child in _children.Values)
        {
            foreach (var item in child.TotalRaw())
            {
                if (!result.ContainsKey(item.Key))
                    result[item.Key] = 0;

                result[item.Key] += item.Value;
            }
        }

        return result;
    }
}

