namespace GhumoOdisha.Application.Auth;

public interface IPinHasher
{
    string Hash(string plainText);

    bool Verify(string hash, string plainText);
}
