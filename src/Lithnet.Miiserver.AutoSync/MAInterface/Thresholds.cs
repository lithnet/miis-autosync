using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public class Thresholds
    {
        [DataMember(Name = "deletes")]
        public int Deletes { get; set; }

        [DataMember(Name = "renames")]
        public int Renames { get; set; }

        [DataMember(Name = "adds")]
        public int Adds { get; set; }

        [DataMember(Name = "updates")]
        public int Updates { get; set; }

        [DataMember(Name = "delete-adds")]
        public int DeleteAdds { get; set; }

        [DataMember(Name = "changes")]
        public int Changes { get; set; }
    }
}
