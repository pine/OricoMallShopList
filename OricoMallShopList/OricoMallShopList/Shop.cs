using System.Runtime.Serialization;

namespace OricoMallShopList
{
    [DataContract]
    public class Shop
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "hostName")]
        public string HostName { get; set; }

        [DataMember(Name = "oricoMallUrl")]
        public string OricoMallUrl { get; set; }
    }
}
