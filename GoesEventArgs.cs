using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GOES_Application
{
    public class GoesEventArgs : EventArgs
    {
        public String GoesResult { get; set; }
    }
    public delegate void CrawlCompleteEventHandler(Object sender, GoesEventArgs e);

}
