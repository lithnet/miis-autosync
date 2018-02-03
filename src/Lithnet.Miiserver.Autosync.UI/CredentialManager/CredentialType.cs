namespace Lithnet.Miiserver.AutoSync.UI
{
    public enum CredentialType
    {
        Generic = 1,
        DomainPassword = 2,
        DomainCertificate = 3,
        DomainVisiblePassword = 4,
        GenericCertificate = 5,
        DomainExtended = 6,
        Maximum = 7,
        MaximumEx = Maximum + 1000,
    }
}
