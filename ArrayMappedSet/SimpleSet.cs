using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArrayMappedSet
{
    /// <summary>
    /// A simple immutable set.
    /// </summary>
    /// <typeparam name="T">The type of set elements.</typeparam>
    public struct SimpleSet<T> : IEnumerable<T>
    {
        // 1. when children != null, then either a collision node or an internal set node
        //   a. collision nodes have bitmap == 0
        //   b. internal nodes have bitmap != 0
        // 2. when children is null, then either an empty tree or a leaf node
        //   a. empty trees have bitmap == 0
        //   b. leaf nodes have bitmap == IS_LEAF  (or really, any non-zero value)
        uint bitmap;
        T value;
        SimpleSet<T>[] children;

        // the hash trie parameters
        const uint IS_LEAF = 0xFFFFFFFF;
        const uint IS_COLLISION = 0x0;
        const int BITS = 5;
        const uint BMASK = (1 << BITS) - 1;

        // if T implements IEquatable, then use that for equality checks via an open instance
        // delegate instead of defaulting to EqualityComparer<T>.Default
        static Func<T, T, bool> eq = typeof(T).IsClass && typeof(IEquatable<T>).IsAssignableFrom(typeof(T))
                                   ? Delegate.CreateDelegate(typeof(Func<T, T, bool>), null, typeof(T).GetMethod("Equals", new[] { typeof(T) })) as Func<T, T, bool>
                                   : EqualityComparer<T>.Default.Equals;

        /// <summary>
        /// A set with a single value.
        /// </summary>
        /// <param name="value"></param>
        public SimpleSet(T value)
        {
            this.bitmap = IS_LEAF;
            this.value = value;
            this.children = null;
        }

        SimpleSet(uint bitmap, params SimpleSet<T>[] children)
        {
            this.bitmap = bitmap;
            this.value = default(T);
            this.children = children;
        }

        /// <summary>
        /// Construct a set from a sequence of values.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static SimpleSet<T> Create(IEnumerable<T> values)
        {
            var tmp = new SimpleSet<T>();
            foreach (var x in values) tmp = tmp.Add(x);
            return tmp;
        }

        /// <summary>
        /// The empty set.
        /// </summary>
        public static SimpleSet<T> Empty
        {
            get { return default(SimpleSet<T>); }
        }

        /// <summary>
        /// True if the set has only a single element.
        /// </summary>
        public bool IsValue
        {
            get { return children == null && bitmap != 0; }
        }

        /// <summary>
        /// True if the set is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return children == null && bitmap == 0; }
        }

        /// <summary>
        /// The value of a single element set.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the set has more than one value.</exception>
        public T Value
        {
            get
            {
                if (IsValue) return value;
                throw new InvalidOperationException("Set is not a single value!");
            }
        }

        /// <summary>
        /// Add an element to the set.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <returns>A set with the new value.</returns>
        public SimpleSet<T> Add(T value)
        {
            return Add(value, Hash(value), 0);
        }

        SimpleSet<T> Add(T value, uint hash, int level)
        {
            // if children == null, then this is either a leaf or an empty tree
            if (children == null)
            {
                // if bitmap is zero, then empty tree, else it has a value that we check for equality
                return bitmap == 0           ? new SimpleSet<T>(value):
                       eq(value, this.value) ? this:
                                               Build(hash, value, Hash(this.value) >> level, this.value);
            }
            // children != null, so if bitmap == 0, then we're at a collision node so simply add to the children if item not already present
            if (bitmap == 0)
            {
                // if hash matches the child hashes, then simply add to the existing collision node
                // else, push this collision node down one level and add the new value
                var chash = Hash(children[0].value) >> level;
                return chash != hash         ? Push(hash, value, chash, ref this):
                       Contains(value, hash) ? this:
                                               new SimpleSet<T>(IS_COLLISION, children.Concat(new[] { new SimpleSet<T>(value) }).ToArray());
            }
            // some internal tree node, so check at what index we will find the node
            var bit = ComputeBit(hash);
            var i = Index(bit, bitmap);
            if (Exists(bit, bitmap))
            {
                var x = children[i].Add(value, hash >> BITS, level + BITS);
                if (/*x.bitmap == bitmap &&*/ x.children == children)
                    return this;
                var nchild = new SimpleSet<T>[children.Length];
                Array.Copy(children, nchild, nchild.Length);
                nchild[i] = x;
                return new SimpleSet<T>(bitmap, nchild);
            }
            else
            {
                var nchild = new SimpleSet<T>[children.Length + 1];
                Array.Copy(children, 0, nchild, 0, i);
                nchild[i] = new SimpleSet<T>(value);
                if (i < children.Length) Array.Copy(children, i, nchild, i + 1, children.Length - i);
                return new SimpleSet<T>(bitmap | bit, nchild);
            }
        }

        /// <summary>
        /// Should only be called on value nodes.
        /// </summary>
        static SimpleSet<T> Build(uint xhash, T xvalue, uint yhash, T yvalue)
        {
            // if hashes are equal at this level, then simply return a collision node
            if (xhash == yhash) return new SimpleSet<T>(IS_COLLISION, new SimpleSet<T>(xvalue), new SimpleSet<T>(yvalue));
            var xbit = ComputeBit(xhash);
            var ybit = ComputeBit(yhash);
            if (xbit == ybit) return new SimpleSet<T>(xbit, Build(xhash >> BITS, xvalue, yhash >> BITS, yvalue));
            var nchild = xbit < ybit ? new[] { new SimpleSet<T>(xvalue), new SimpleSet<T>(yvalue) }:
                                       new[] { new SimpleSet<T>(yvalue), new SimpleSet<T>(xvalue) };
            return new SimpleSet<T>(xbit | ybit, nchild);
        }

        /// <summary>
        /// Should only be called on collision nodes where the hashes differ. Pushes the collision node further
        /// down the tree until the hash bits branch.
        /// </summary>
        static SimpleSet<T> Push(uint xhash, T xvalue, uint chash, ref SimpleSet<T> collision)
        {
            // if hashes are equal at this level, then simply return a collision node
            var xbit = ComputeBit(xhash);
            var ybit = ComputeBit(chash);
            if (xbit == ybit) return new SimpleSet<T>(xbit, Push(xhash >> BITS, xvalue, chash >> BITS, ref collision));
            var nchild = xbit < ybit ? new[] { new SimpleSet<T>(xvalue), collision }:
                                       new[] { collision, new SimpleSet<T>(xvalue) };
            return new SimpleSet<T>(xbit | ybit, nchild);
        }

        /// <summary>
        /// The union of two sets.
        /// </summary>
        /// <param name="other">The set to merge.</param>
        /// <returns>The union of this set, and <paramref name="other"/>.</returns>
        public SimpleSet<T> Union(SimpleSet<T> other)
        {
            return Union(ref other, 0);
        }
        
        SimpleSet<T> Union(ref SimpleSet<T> other, int level)
        {
            // if either set is a simple value, or empty, then return the other set with this value
            if (other.children == null)
            {
                return other.bitmap == 0 ? this : Add(other.value, Hash(other.value) >> level, level);
            }
            else if (children == null)
            {
                return bitmap == 0 ? other : other.Add(value, Hash(value) >> level, level);
            }
            else if (bitmap == 0) // collision node
            {
                // other may be collision node or internal node
                //FIXME: a more efficient algorithm would use a variant of Push()
                var tmp = other;
                foreach (var x in children) tmp = tmp.Add(x.value, Hash(x.value) >> level, level);
                return tmp;
            }
            else if (other.bitmap == 0)
            {
                return other.Union(ref this, level);
            }
            // both sets have children, so the union of the two sets is defined by the bitwise
            // OR of the bitmaps, and the size of the array is the bitcount of that bitmap
            var ubitmap = bitmap | other.bitmap;
            var uchildren = new SimpleSet<T>[BitCount(ubitmap)];
            for (uint bit = 1; bit != 0; bit <<= 1)
            {
                if (Exists(bit, ubitmap))
                {
                    var i = Index(bit, ubitmap);
                    uchildren[i] = !Exists(bit, bitmap)      ? other.children[Index(bit, other.bitmap)]:
                                   !Exists(bit, other.bitmap)? children[Index(bit, bitmap)]:
                                                               children[Index(bit, bitmap)].Union(ref other.children[Index(bit, other.bitmap)], level + BITS);
                }
            }
            return new SimpleSet<T>(ubitmap, uchildren);
        }

        /// <summary>
        /// Remove an element from the set.
        /// </summary>
        /// <param name="value">The value to remove.</param>
        /// <returns>A set without the given value.</returns>
        public SimpleSet<T> Remove(T value)
        {
            return Remove(value, Hash(value));
        }

        SimpleSet<T> Remove(T value, uint hash)
        {
            if (children == null)   // value or empty node
            {
                return bitmap != 0 && eq(this.value, value) ? Empty : this;
            }
            else if (bitmap == 0)   // collision node
            {
                if (children.Length == 2)
                {
                    var i = eq(value, children[0].value) ?  1:
                            eq(value, children[1].value) ?  0:
                                                           -1;
                    return i < 0 ? this : new SimpleSet<T>(children[i].value);
                }
                for (var i = 0; i < children.Length; ++i)
                {
                    if (eq(value, children[i].value))
                    {
                        var nchildren = new SimpleSet<T>[children.Length - 1];
                        Array.Copy(children, 0, nchildren, 0, i);
                        Array.Copy(children, i + 1, nchildren, i, nchildren.Length - i);
                        return new SimpleSet<T>(IS_COLLISION, nchildren);
                    }
                }
            }
            else
            {
                var bit = ComputeBit(hash);
                if (Exists(bit, bitmap))
                {
                    var i = Index(bit, bitmap);
                    var x = children[i].Remove(value, hash >> BITS);
                    SimpleSet<T>[] nchildren;
                    uint nbitmap;
                    if (!x.IsEmpty)
                    {
                        nchildren = new SimpleSet<T>[children.Length];
                        Array.Copy(children, 0, nchildren, 0, nchildren.Length);
                        nchildren[i] = x;
                        nbitmap = bitmap;
                    }
                    else if (children.Length > 1)   // empty child set, but elements remain
                    {
                        nchildren = new SimpleSet<T>[children.Length - 1];
                        Array.Copy(children, 0, nchildren, 0, i);
                        Array.Copy(children, i + 1, nchildren, i, nchildren.Length - i);
                        nbitmap = bitmap & ~bit;
                    }
                    else
                    {
                        return Empty;
                    }
                    return new SimpleSet<T>(nbitmap, nchildren);
                }
            }
            return this;
        }

        /// <summary>
        /// The intersection of two sets.
        /// </summary>
        /// <param name="other">The set with which to intersect.</param>
        /// <returns>The intersection of this set and <paramref="other"/>.</returns>
        public SimpleSet<T> Intersect(SimpleSet<T> other)
        {
            return Intersect(ref other, 0);
        }

        SimpleSet<T> Intersect(ref SimpleSet<T> other, int level)
        {
            // if either set is a simple value, or empty, then return the other set with this value
            if (other.children == null)
            {
                return other.bitmap != 0 && Contains(other.value, Hash(other.value) >> level) ? other : Empty;
            }
            else if (children == null)
            {
                return bitmap != 0 && other.Contains(value, Hash(value) >> level) ? this : Empty;
            }
            else if (bitmap == 0) // collision node
            {
                //FIXME: a more efficient algorithm should be possible
                var tmp = new SimpleSet<T>();
                foreach (var x in children)
                {
                    if (other.Contains(x.value)) tmp = tmp.Add(x.value, Hash(x.value) >> level, level);
                }
                return tmp;
            }
            else if (other.bitmap == 0)
            {
                return other.Intersect(ref this, level);
            }
            // both sets have children, so the intersection of the two sets is defined by the bitwise
            // AND of the bitmaps, and the size of the array is the bitcount of that bitmap
            var ibitmap = bitmap & other.bitmap;
            var ichildren = new SimpleSet<T>[BitCount(ibitmap)];
            for (var bit = (uint)1; bit > 0; bit <<= 1)
            {
                if (Exists(bit, ibitmap))
                {
                    var i = Index(bit, ibitmap);
                    ichildren[i] = children[Index(bit, bitmap)].Intersect(ref other.children[Index(bit, other.bitmap)], level + BITS);
                }
            }
            return new SimpleSet<T>(ibitmap, ichildren);
        }

        /// <summary>
        /// Checks the set for an element.
        /// </summary>
        /// <param name="value">The value to check for.</param>
        /// <returns>True if the value is in the set, false otherwise.</returns>
        public bool Contains(T value)
        {
            return Contains(value, Hash(value));
        }

        bool Contains(T value, uint hash)
        {
            if (children == null) return bitmap != 0 && eq(value, this.value);
            if (bitmap == 0) return children.Any(x => eq(x.value, value)); // collision node
            var bit = ComputeBit(hash);
            var i = Index(bit, bitmap);
            return Exists(bit, bitmap) && children[i].Contains(value, hash >> BITS);
        }

        /// <summary>
        /// Obtain an enumerator for the set.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            var en = children != null ? children.SelectMany(x => x):
                     bitmap == IS_LEAF? new[] { value } as IEnumerable<T>:
                                        Enumerable.Empty<T>();
            return en.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Create a string representation of the set.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return bitmap == 0 && children != null ? children.Aggregate("{", (acc, x) => acc + "," + x) + "}":
                   bitmap == 0                     ? "{}":
                   children != null                ? base.ToString():
                                                     "{" + value + "}";
        }

        static uint Hash(T value)
        {
            return unchecked((uint)value.GetHashCode());
        }

        static uint ComputeBit(uint hash)
        {
            return (uint)1 << unchecked((int)(hash & BMASK));
        }

        static int Index(uint bit, uint bitmap)
        {
            return BitCount(bitmap & (bit - 1));
        }

        static int BitCount(uint value)
        {
            value = value - ((value >> 1) & 0x55555555);                    // reuse input as temporary
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);     // temp
            value = ((value + (value >> 4) & 0xF0F0F0F) * 0x1010101) >> 24; // count
            return unchecked((int)value);
        }

        /// <summary>
        /// True if the <paramref name="bit"/> is set in <paramref name="bitmap"/>.
        static bool Exists(uint bit, uint bitmap)
        {
            return (bitmap & bit) != 0;
        }
    }
}
