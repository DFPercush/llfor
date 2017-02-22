using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace llfor
{
    class stdinEnumerable : IEnumerable<string>
    {
        public IEnumerator<string> GetEnumerator()
        {
            string line = "";
            while ((line = Console.ReadLine()) != null)
            {
                yield return line;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class fileLineEnumerable : IEnumerable<string>
    {
        StreamReader sr;
        private fileLineEnumerable() { }
        public fileLineEnumerable(string filenameParam)
        {
            sr = new StreamReader(filenameParam);
        }
        public IEnumerator<string> GetEnumerator()
        {
            string line = "";
            while ((line = sr.ReadLine()) != null)
            {
                yield return line;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
