namespace MpParserAPI.Models.Dtos
{
    public class LoginToSessionDto
    {
        public Guid SessionKey { get; set; }

        public string SessionPassword { get; set; }    
    }
}
