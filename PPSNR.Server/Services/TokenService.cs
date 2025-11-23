namespace PPSNR.Server.Services;

public class TokenService
{
    public Guid NewToken() => Guid.NewGuid();

    public bool IsValid(Guid token) => token != Guid.Empty;
}
