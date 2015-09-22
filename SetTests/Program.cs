using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using ArrayMappedSet;

namespace SetTests
{
    class Program
    {
        static void CheckSimple()
        {
            var set = new SimpleSet<int>().Add(1).Add(5).Add(1).Add(5).OrderBy(x => x);
            Debug.Assert(set.SequenceEqual(new[] { 1, 5 }));
        }
        static void CheckUnion()
        {
            var set1 = new SimpleSet<int>().Add(1).Add(5).Add(1).Add(5);
            var set2 = new SimpleSet<int>().Add(1).Add(7).Add(29);
            Debug.Assert(set1.Union(set2).OrderBy(x => x).SequenceEqual(new[] { 1, 5, 7, 29 }));
        }
        static void CheckIntersect()
        {
            var set1 = new SimpleSet<int>().Add(1).Add(5).Add(1).Add(5);
            var set2 = new SimpleSet<int>().Add(1).Add(7).Add(29);
            Debug.Assert(set1.Intersect(set2).OrderBy(x => x).SequenceEqual(new[] { 1 }));
        }
        static void CheckAll(IEnumerable<int> data)
        {
            var set1 = SimpleSet<int>.Create(data);
            Debug.Assert(set1.OrderBy(x => x).SequenceEqual(data.Distinct().OrderBy(x => x)));
            Debug.Assert(set1.Union(set1).OrderBy(x => x).SequenceEqual(data.Distinct().OrderBy(x => x)));
        }
        static void CheckCollision()
        {
            var set = new SimpleSet<long>().Add(1).Add(2 * (int.MaxValue + 1L));
            Debug.Assert(set.OrderBy(x => x).SequenceEqual(new[] { 1L, 2 * (int.MaxValue + 1L) }));

            var set2 = new SimpleSet<long>().Add(2).Add(1 + 2 * (int.MaxValue + 2L));
            Debug.Assert(set2.OrderBy(x => x).SequenceEqual(new[] { 2L, 1 + 2 * (int.MaxValue + 2L) }));

            var union = set.Union(set2);
            Debug.Assert(union.OrderBy(x => x).SequenceEqual(new[] { 1L, 2L, 2 * (int.MaxValue + 1L), 1 + 2 * (int.MaxValue + 2L) }));
        }
        static void CheckAll(int[] data1, int[] data2)
        {
            var set1 = SimpleSet<int>.Create(data1);
            var set2 = SimpleSet<int>.Create(data2);
            Debug.Assert(set1.OrderBy(x => x).SequenceEqual(data1.Distinct().OrderBy(x => x)));
            Debug.Assert(set2.OrderBy(x => x).SequenceEqual(data2.Distinct().OrderBy(x => x)));
            var union = set1.Union(set2).OrderBy(x => x).ToList();
            var source = data1.Concat(data2).Distinct().OrderBy(x => x).ToList();
            if (!union.SequenceEqual(source))
            {
                source.Add(-1);
                System.IO.File.WriteAllText("sequence.csv", union.Zip(source, (x, y) => Tuple.Create(x, y)).Aggregate(new StringBuilder(), (acc, x) => acc.Append(x.Item1).Append(',').Append(x.Item2).AppendLine()).ToString());
                System.IO.File.WriteAllText("data.csv", data1.Zip(data2, (x, y) => Tuple.Create(x, y)).Aggregate(new StringBuilder(), (acc, x) => acc.Append(x.Item1).Append(',').Append(x.Item2).AppendLine()).ToString());
                Debug.Assert(false);
            }
            //Debug.Assert(union.SequenceEqual(source));
            Debug.Assert(set1.Intersect(set2).OrderBy(x => x).SequenceEqual(data1.Intersect(data2).Distinct().OrderBy(x => x)));
        }
        static Random rand = new Random();
        static int[] Generate(int count, int upper = int.MaxValue)
        {
            var x = new int[count];
            for (var i = 0; i < count; ++i)
            {
                x[i] = rand.Next(0, upper);
            }
            return x;
        }
        static void Main(string[] args)
        {
            CheckSimple();
            CheckUnion();
            CheckIntersect();
            CheckCollision();

            // failing boundary case
            var tmp = Generate(33);
            CheckAll(tmp.Concat(new[] { tmp[0] }));

            // test huge numbers
            //CheckAll(Generate(65537 * 10, 100));
            //CheckAll(Generate(128350));
            //CheckAll(Generate(65000), Generate(65537));
            while (true)
            {
                CheckAll(Generate(65000), Generate(650000));
            }
            //var csv = System.IO.File.ReadAllLines("data.csv").Select(x => x.Split(','));
            //var data1 = csv.Select(x => int.Parse(x[0])).ToArray();
            //var data2 = csv.Select(x => int.Parse(x[1])).ToArray();
            //CheckAll(data1, data2);
        }
    }
}
