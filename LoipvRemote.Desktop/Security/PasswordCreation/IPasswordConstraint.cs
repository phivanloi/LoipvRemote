using System.Security;


namespace LoipvRemote.Security.PasswordCreation
{
    public interface IPasswordConstraint
    {
        string ConstraintHint { get; }

        bool Validate(SecureString password);
    }
}