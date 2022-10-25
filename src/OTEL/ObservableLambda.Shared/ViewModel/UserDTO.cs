using System.Text.Json.Serialization;

namespace ObservableLambda.Shared.ViewModel;

public class UserDTO
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; }
    
    [JsonPropertyName("emailAddress")]
    public string EmailAddress { get; set; }
}