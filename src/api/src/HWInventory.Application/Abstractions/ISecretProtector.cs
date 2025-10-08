namespace HWInventory.Application.Abstractions;

public interface ISecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}
