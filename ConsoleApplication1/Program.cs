using System;
using System.Collections.Generic;

namespace ConsoleApplication1
{
    class Program
    {

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

        static void Main(string[] args)
        {

            var list = new List<Record>
            {
                new Record("Bob", 20),
                new Record("Alice", 31),
                new Record("Ricky", 19),
                new Record("Doofus", 24)
            };

            var calc = new Calc<Record>(new Dictionary<string, string> { { "Id", "id" }, { "Age", "age" } }, null, true);

            var total = calc.Do("NEG(9) + SUM(\"Age\"[\"Id\" <= 2][20 <= this]) / 2", list);

            Console.WriteLine(total);
        }
    }
}
