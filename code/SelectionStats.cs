using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Transit_Scope.code
{
    [DataContract]
    //不准继承，只在当前程序集使用
    internal sealed class StatItem
    {
        [DataMember(Name = "labelKey")]
        public string LabelKey { get; set; }

        [DataMember(Name = "label")]
        public string Label { get; set; }

        [DataMember(Name = "value")]
        public int Value { get; set; }

        [DataMember(Name = "color")]
        public string Color { get; set; }
    }

    [DataContract]
    internal sealed class SelectionStats
    {
        [DataMember(Name = "titleKey")]
        public string TitleKey { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "subtitleKey")]
        public string SubtitleKey { get; set; }

        [DataMember(Name = "subtitleArg")]
        public string SubtitleArg { get; set; }

        [DataMember(Name = "subtitle")]
        public string Subtitle { get; set; }

        [DataMember(Name = "total")]
        public int Total { get; set; }

        [DataMember(Name = "displayTotal")]
        public int DisplayTotal { get; set; }

        [DataMember(Name = "items")]
        public List<StatItem> Items { get; set; } = new();

        public string ToJson()
        {
            DataContractJsonSerializer serializer = new(typeof(SelectionStats));

            using MemoryStream stream = new();
            serializer.WriteObject(stream, this);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
