using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace FitBiteApp
{
    [DataContract]
    class Event
    {
    
        [DataMember]
        public int PartitionId { get; set; }
        [DataMember]
        public DateTime TimeStamp1 { get; set; }
        [DataMember]
        public double Temperature { get; set; }
        [DataMember]
        public double Light { get; set; }
        [DataMember]
        public double Sound { get; set; }
        [DataMember]
        public int IOT_DEVICE_ID { get; set; }
    }
}
