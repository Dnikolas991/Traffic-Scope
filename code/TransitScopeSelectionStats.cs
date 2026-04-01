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
        // 本地化键，由前端根据当前语言解析。
        [DataMember(Name = "labelKey")]
        public string LabelKey { get; set; }

        // 回退文本，避免键缺失时前端完全空白。
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

        // total 用于图表计算。
        [DataMember(Name = "total")]
        public int Total { get; set; }

        // displayTotal 专门给 UI 中心数值展示。
        // 没有流量时这里保持 0，避免 UI 中心错误显示 1。
        [DataMember(Name = "displayTotal")]
        public int DisplayTotal { get; set; }

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
