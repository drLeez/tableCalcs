using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1 {
    class Program {
        
        internal class Record
        {
            static int ID = 0;

            public int id, age;
            public string name;

            public Record(string n, int a)
            {
                id = ID++;
                name = n;
                age = a;
            }
        }
        
        static void Main(string[] args) {

            var list = new List<Record>();
            list.Add(new Record("Bob", 20));
            list.Add(new Record("Alice", 31));
            list.Add(new Record("Ricky", 19));
            list.Add(new Record("Doofus", 24));

            var calc = new Calc<Record>(new string[] { "id", "age" });

            var total = calc.Do("NEG(9) + SUM(\"age\"[,2,id][20,,age]) / 2", list);
            Console.WriteLine(total);
        }
    }
}
