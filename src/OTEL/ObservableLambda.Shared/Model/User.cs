using System;
using System.Text.Json.Serialization;
using ObservableLambda.Shared.Utilities;

namespace ObservableLambda.Shared.Model;

public class User
{
    [JsonConstructor]
    private User()
    {
    }

    public static User Create(string emailAddress)
    {
        if (string.IsNullOrEmpty(emailAddress))
        {
            throw new ArgumentNullException(nameof(emailAddress));
        }

        var formattedEmail = emailAddress.ToLower().Trim();

        return new User()
        {
            UserId = Utilities.HashGenerator.Base64Encode(formattedEmail),
            EmailAddress = formattedEmail
        };
    }

    public string UserId { get; private set; }

    public string EmailAddress { get; private set; }
}