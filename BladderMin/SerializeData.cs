using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using System.IO;

namespace BladderMin
{
    //create protocol list and activate data after selecting protocol
    public class SerializeData
    {
        public readonly string path = $@"\\sdappvimg010\esapi$\hlowe\BladderMin\Protocols\";
        public List<string> ReadFolder()
        {
            return Directory.GetFiles(path).ToList();
        }
        public Protocol_Preprocessor.BladderminProtocol SerializeProtocol(string file)
        {
            var xmlTools = new XmlTools();
            var serializer = xmlTools.Serializer;
            try
            {
                using (var stream = new StreamReader(file))
                {
                    using (var reader = XmlReader.Create(stream, xmlTools.Settings))
                    {
                        var deserializeFile = (Protocol_Preprocessor.BladderminProtocol)serializer.Deserialize(reader);
                        return deserializeFile;
                    }
                }
            }
            catch (Exception ex) //if we can't read a protocol, it's best to crash out instead of lock the main thread
            {
                throw new Exception($@"{ex.Message}");
            }
        }
    }
}
