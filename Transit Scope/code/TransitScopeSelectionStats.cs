using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Transit_Scope.code
{
    [DataContract]
    internal sealed class TransitScopeStatItem
    {
        [DataMember(Name = "label")]
        public string Label { get; set; }

        [DataMember(Name = "value")]
        public int Value { get; set; }

        [DataMember(Name = "color")]
        public string Color { get; set; }
    }

    [DataContract]
    internal sealed class TransitScopeSelectionStats
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "subtitle")]
        public string Subtitle { get; set; }

        [DataMember(Name = "total")]
        public int Total { get; set; }

        [DataMember(Name = "items")]
        public List<TransitScopeStatItem> Items { get; set; } = new();

        public string ToJson()
        {
            DataContractJsonSerializer serializer = new(typeof(TransitScopeSelectionStats));

            using MemoryStream stream = new();
            serializer.WriteObject(stream, this);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
