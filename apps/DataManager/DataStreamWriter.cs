/// This is the main method that runs in order to create the DataManager. It's a bit complicated because we have to make sure that the order matters
using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Globalization;
using System.Timers;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.Interpolation;
using Libfmax;
using LSL;

namespace DataManager
{
    public class DataStreamWriter
    {

        private DataStream _dataStream;

        private StreamWriter _streamWriter;

        private String _filename;
        private String _filenameMetaData;

        private int _bufferSize;


        public DataStreamWriter(DataStream dataStream, String directory, String subject, String session, int bufferSize)
        {
            _dataStream = dataStream;
            _bufferSize = bufferSize;

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var subjectFormatted = (subject != "") ? $"_{subject}" : "";

            _filename = Path.Combine(directory, $"{session}{subjectFormatted}_{dataStream.Name} ({dataStream.Host})_{dataStream.Type}");
            _filename += ".csv";

            _filenameMetaData = Path.Combine(directory, $"{session}{subjectFormatted}_{dataStream.Name} ({dataStream.Host})_{dataStream.Type}");
            _filenameMetaData += ".json";

            _dataStream.NewDataReceived += NewDataReceived;
        }

        public void StartRecording()
        {
            using (StreamWriter metadataStreamWriter = new StreamWriter(_filenameMetaData, true, Encoding.UTF8, _bufferSize))
            {
                metadataStreamWriter.Write(_dataStream.GetRecordMeta());
            }
        }

        private void WriteRecord(int dataPosition)
        {
            if (_streamWriter == null)
            {
                _streamWriter = new StreamWriter(_filename, true, Encoding.UTF8, _bufferSize);

                string header = _dataStream.GetHeaderData();

                _streamWriter.Write(header);
            }

            var lineToWrite = _dataStream.FormatForOutput(dataPosition);
            _streamWriter.WriteLine(lineToWrite);
        }

        public void EndRecording()
        {
            if (_streamWriter != null)
            {
                _streamWriter.Close();
            }

            _dataStream.ReplaceEndTimeInHeader(_filename);
        }

        public void Flush()
        {
            if (_streamWriter != null)
            {
                _streamWriter.Flush();
            }
        }

        /// <summary>
        /// Called when new data is received. This is the method that will be called by the DataStream.
        /// </summary>
        /// <param name="sender">The sender of the event. It is an instance of DataStream</param>
        /// <param name="index">The index of the data</param>
        public void NewDataReceived(object sender, int index)
        {
            WriteRecord(index);
        }
    }
}

