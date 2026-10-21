using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LiveSplit.Compat.WinForms.Support;

public static class StubSupport
{
    private static readonly ConditionalWeakTable<object, List<object>> _collections = new();
    private static readonly ConditionalWeakTable<object, object> _collectionOwners = new();

    public static void SetCollectionOwner(object collection, object owner)
    {
        if (collection != null && owner != null)
        {
            _collectionOwners.AddOrUpdate(collection, owner);
        }
    }

    public static List<object> GetCollectionList(object collection)
    {
        return _collections.GetOrCreateValue(collection);
    }

    public static int CollectionAdd(object collection, object item)
    {
        var list = GetCollectionList(collection);
        list.Add(item);
        if (item != null)
        {
            SetChildParent(item, collection);
        }
        return list.Count - 1;
    }

    public static void CollectionAddRange(object collection, object items)
    {
        if (items is IEnumerable e)
        {
            foreach (var item in e)
            {
                CollectionAdd(collection, item);
            }
        }
    }

    public static bool CollectionRemove(object collection, object item)
    {
        var list = GetCollectionList(collection);
        bool res = list.Remove(item);
        if (res && item != null)
        {
            ClearChildParent(item);
        }
        return res;
    }

    public static void CollectionRemoveAt(object collection, int index)
    {
        var list = GetCollectionList(collection);
        if (index >= 0 && index < list.Count)
        {
            var item = list[index];
            list.RemoveAt(index);
            if (item != null)
            {
                ClearChildParent(item);
            }
        }
    }

    public static void CollectionClear(object collection)
    {
        var list = GetCollectionList(collection);
        foreach (var item in list)
        {
            if (item != null)
            {
                ClearChildParent(item);
            }
        }
        list.Clear();
    }

    public static int CollectionCount(object collection)
    {
        return GetCollectionList(collection).Count;
    }

    public static bool CollectionContains(object collection, object item)
    {
        return GetCollectionList(collection).Contains(item);
    }

    public static int CollectionIndexOf(object collection, object item)
    {
        return GetCollectionList(collection).IndexOf(item);
    }

    public static object CollectionGetItem(object collection, int index)
    {
        var list = GetCollectionList(collection);
        if (index >= 0 && index < list.Count) return list[index];
        return null;
    }

    public static void CollectionSetItem(object collection, int index, object value)
    {
        var list = GetCollectionList(collection);
        if (index >= 0 && index < list.Count)
        {
            list[index] = value;
        }
    }

    public static IEnumerator CollectionGetEnumerator(object collection)
    {
        return GetCollectionList(collection).GetEnumerator();
    }

    public static void CollectionInsert(object collection, int index, object item)
    {
        var list = GetCollectionList(collection);
        if (index >= 0 && index <= list.Count)
        {
            list.Insert(index, item);
            if (item != null)
            {
                SetChildParent(item, collection);
            }
        }
    }

    private static void SetChildParent(object child, object collection)
    {
        try
        {
            if (!_collectionOwners.TryGetValue(collection, out object owner) || owner == null)
            {
                var ownerProp = collection.GetType().GetProperty("Owner", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                owner = ownerProp?.GetValue(collection);
            }
            if (owner != null)
            {
                var parentProp = child.GetType().GetProperty("Parent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                parentProp?.SetValue(child, owner);
            }
        }
        catch { }
    }

    private static void ClearChildParent(object child)
    {
        try
        {
            var parentProp = child.GetType().GetProperty("Parent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            parentProp?.SetValue(child, null);
        }
        catch { }
    }

    // Delegate Invocation
    public static object InvokeDelegate(Delegate d, object[] args)
    {
        return d?.DynamicInvoke(args);
    }

    // Clamping
    public static decimal ClampNumeric(decimal val, decimal min, decimal max)
    {
        if (min > max) max = min;
        return Math.Max(min, Math.Min(max, val));
    }

    // Clipboard
    private static string _clipboard = "";
    public static void SetClipboardText(string text) => _clipboard = text ?? "";
    public static string GetClipboardText() => _clipboard;
    public static bool ContainsClipboardText() => !string.IsNullOrEmpty(_clipboard);
    public static void ClearClipboard() => _clipboard = "";

    // Application
    public static string StartupPath => AppContext.BaseDirectory;
    public static string ExecutablePath => Path.Combine(AppContext.BaseDirectory, "LiveSplit.dll");
    public static string ProductVersion => "1.0.0";

    // Event firing
    public static void FireEvent(MulticastDelegate d, object sender, EventArgs args)
    {
        d?.DynamicInvoke(sender, args ?? EventArgs.Empty);
    }

    // ComboBox / ListBox selection
    public static object GetSelectedItem(object selectedItemValue, int selectedIndex, object items)
    {
        if (selectedItemValue != null) return selectedItemValue;
        if (items != null && selectedIndex >= 0)
        {
            var list = GetCollectionList(items);
            if (selectedIndex < list.Count) return list[selectedIndex];
        }
        return null;
    }
}
