using System;

namespace CloudBuildPlugin.Common
{
    public class CosFileExistsException : Exception
    {
//        public string Message;
        public CosFileExistsException(string msg)
            :base(msg)
        {
            
        }
    }
}