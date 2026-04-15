namespace doantotnghiep_api.Services
{
    public interface ICaptchaService
    {
        string GenerateCaptcha();
        string StoreAndGetSessionId(string code);
        bool ValidateCaptcha(string sessionId, string inputCode);
        string GenerateCaptchaImage(string code);
    }
}
