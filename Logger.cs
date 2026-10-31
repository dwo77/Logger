using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace dwo
{
    internal sealed class Logger
    {
        #region Members
        private static volatile Logger _instance;
        private static object _instanceLock = new object();
        private string _path = "";
        private string _configFileName = "";
        private string _logFileName = "";
        private ushort _level = 3;
        private long _maxLength = 1024 * 1024;
        private int _numBackups = 5;
        private DateTime _lastEvent = DateTime.MinValue;
        private object _lockObject = new object();
        private FileStream _logFile;
        private FileSystemWatcher _watcher = new FileSystemWatcher();
        private enum LogLevel : ushort { All = 5, Debug = 4, Info = 3, Error = 2, Fatal = 1 };
        #endregion

        private void UpdateConfig()
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(Path.Combine(_path,_configFileName));

            if(string.IsNullOrWhiteSpace(_logFileName))
                _logFileName = GetXmlValue(xml.DocumentElement,"appSettings/add[@key='LogFileName']","log4net/appender/param[@name='File']","value","Recorder.log");

            _level = (ushort) StringToLevel(GetXmlValue(xml.DocumentElement,"appSettings/add[@key='LogLevel']","log4net/root/level","value","INFO"));

            string max = GetXmlValue(xml.DocumentElement,"appSettings/add[@key='LogFileSize']","log4net/appender/param[@name='MaximumFileSize']","value","1000");

            Regex rx = new Regex(@"(\d+)([KM]B)");
            var matches = rx.Match(max);

            if(int.TryParse(matches.Groups[1].Value,out int n))
            {
                if(matches.Groups[2].Value == "MB")
                    _maxLength = n * 1024 * 1024;
                else
                    _maxLength = n * 1024;
            }
            else
            {
                _maxLength = 1024 * 1024;
            }

            string backups = GetXmlValue(xml.DocumentElement,"appSettings/add[@key='LogBackups']","log4net/appender/param[@name='MaxSizeRollBackups']","value","5");
            int.TryParse(backups,out _numBackups);
        }
        private void RollBackups()
        {
            _logFile.Close();
            _logFile.Dispose();

            for(int i = _numBackups - 1;i > 0;i--)
            {
                string fn = Path.Combine(_path,$"{_logFileName}.{i}");

                if(File.Exists(fn))
                    File.Copy(fn,Path.Combine(_path,$"{_logFileName}.{i + 1}"),true);
            }

            File.Copy(Path.Combine(_path,_logFileName),Path.Combine(_path,_logFileName + ".1"),true);
            File.Delete(_logFileName);

            _logFile = new FileStream(Path.Combine(_path,_logFileName),FileMode.Append,
                FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);
        }
        private void Write(LogLevel level,string line)
        {
            lock(_lockObject)
            {
                string s = string.Format("{0} {1} {2}\r\n",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff"),LevelToString(level),line);

                if(_logFile == null)
                    return;

                try
                {
                    _logFile.Write(Encoding.GetEncoding(28591).GetBytes(s),0,s.Length);
                    _logFile.Flush();

                    if(_logFile.Length >= _maxLength)
                        RollBackups();
                }
                catch(Exception) { }
            }
        }
        private void OnConfigChanged(object source, FileSystemEventArgs e)
        {
            long t = DateTime.Now.Ticks - _lastEvent.Ticks;
            
            if(t > 100000000)
            {
                _lastEvent = DateTime.Now;
                UpdateConfig();
            }
        }
        private static string LevelToString(LogLevel level)
        {
            switch(level)
            {
                case LogLevel.All:   return "ALL  ";
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Info:  return "INFO ";
                case LogLevel.Error: return "ERROR";
                case LogLevel.Fatal: return "FATAL";
            }

            return "?????";
        }
        private static LogLevel StringToLevel(string s)
        {
            switch(s.ToLower())
            {
                case "all": return LogLevel.All;
                case "debug": return LogLevel.Debug;
                case "info": return LogLevel.Info;
                case "error": return LogLevel.Error;
                case "fatal": return LogLevel.Fatal;
            }

            return LogLevel.Info;
        }
        private static string GetXmlValue(XmlNode node,string xpath,string xpath2,string attribute,string defval = "")
        {
            XmlNode sub = node.SelectSingleNode(xpath);

            if(sub == null)
            {
                sub = node.SelectSingleNode(xpath2);

                if(sub == null)
                    return defval;
            }

            return sub.Attributes[attribute]?.Value.ToString().Trim() ?? defval;
        }
        private Logger() { }

        public static Logger Instance
        {
            get
            {
                if(_instance == null)
                {
                    lock(_instanceLock)
                    {
                        if(_instance == null)
                            _instance = new Logger();
                    }
                }

                return _instance;
            }
        }
        public bool Initialize(string path,string configFileName)
        {
            try
            {
                _path = path;
                _configFileName = configFileName;
                UpdateConfig();

                _watcher.Path = _path;
                _watcher.Filter = _configFileName;
                _watcher.NotifyFilter = NotifyFilters.LastWrite;
                _watcher.Changed += OnConfigChanged;
                _watcher.EnableRaisingEvents = true;

                _logFile = new FileStream(Path.Combine(_path,_logFileName),FileMode.Append,
                    FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);
            }
            catch(Exception)
            {
                return false;
            }

            return true;
        }
        public void Stop()
        {
            _level = 0;

            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnConfigChanged;
            _watcher.Dispose();

            _logFile.Close();
            _logFile.Dispose();
        }
        public void Debug(string s) 
        {
            if(IsDebugEnabled)
                Write(LogLevel.Debug,s);
        }
        public void Info(string s) 
        { 
            if(IsInfoEnabled)
                Write(LogLevel.Info,s);
        }
        public void Error(string s) 
        { 
            if(IsErrorEnabled)
                Write(LogLevel.Error,s);
        }
        public void Fatal(string s) 
        {
            if(IsFatalEnabled)
                Write(LogLevel.Fatal,s);
        }

        public bool IsDebugEnabled { get { return _level >= (ushort) LogLevel.Debug; } }
        public bool IsInfoEnabled  { get { return _level >= (ushort) LogLevel.Info;  } }
        public bool IsErrorEnabled { get { return _level >= (ushort) LogLevel.Error; } }
        public bool IsFatalEnabled { get { return _level >= (ushort) LogLevel.Fatal; } }
    }
}
