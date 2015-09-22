# Fast, Compact Immutable Set

This provides a hash array mapped set, based on the well known hash array mapped trie.

It utilizes a struct encoding for compactness:

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
		...
	}

A unique property of this struct encoding is that single-element sets are just as fast
and compact as inlining the value itself into its enclosing context, at least when T
is a reference type. For instance, if you have a field of type T, a field of
type SimpleSet<T> containing a single value is just as compact and efficient.

# Future Work

I plan to add a few variants and benchmark them against each other to see which one
is truly more compact and efficient overall:

 1. Change the "T value" field to "T[] values". In this encoding, if children and values
    are both null, then the set is empty; if children is not null, then bitmap encodes
	the entries in children; if values is not null, then the tree is height 1
	and bitmap encodes the entries in values, and when we add an element to an index
	that has an entry, we promote the whole node into the children array.
 2. Take the encoding in #1, and note that we can inline the T[] array one level *up* the
	tree if we keep separate bitmaps for values and children. So subtrees with only a single
	value are kept in the values array, until a new value is added at which point we promote
	it to the children array. This is essentially the representation which Jules Jacobs devised
	that is used in Sasa's trie [1]. It's much more compact for large trees because there
	are many more leaves than internal nodes, and leaves are encoded as T[] instead of
	Set<T>[], which has many unused headers. With a suitable encoding using generics, we
	can compact this even further for the final level of the tree.

[1] https://sourceforge.net/p/sasa/code/ci/default/tree/Sasa.Collections/Trie.cs

# License

LGPL v2.1