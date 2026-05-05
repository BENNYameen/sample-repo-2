using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Repositories;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<string, User> _users = new(StringComparer.Ordinal);

    public User? GetById(string userId)
    {
        _users.TryGetValue(userId, out var u);
        return u;
    }

    public void Save(User user) => _users[user.Id] = user;
}
