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

            var calc = new Calc<Record>(null, true);
            var total = calc.Do("NEG(9) + SUM('age'[ 'name' = \"Ricky\" || 'name' = \"Doofus\" ]) / 2", list);
            Console.WriteLine(total);
        }
    }
}
