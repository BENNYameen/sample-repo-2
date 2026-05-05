using WhiteboxMetrix.Models;

namespace WhiteboxMetrix.Repositories;

public interface IUserRepository
{
    User? GetById(string userId);
    void Save(User user);
}
