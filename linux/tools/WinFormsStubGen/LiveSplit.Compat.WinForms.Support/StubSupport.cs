using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace LiveSplit.Compat.WinForms.Support;

public static class StubSupport
{
    private static readonly ConditionalWeakTable<object, List<object>> _collections = new();
    private static readonly ConditionalWeakTable<object, object> _collectionOwners = new();
    private static readonly ConditionalWeakTable<object, Dictionary<string, object>> _propertyStore = new();
    private static readonly ConditionalWeakTable<object, object> _bindingSourceLists = new();

    private sealed class EventKeyComparer : IEqualityComparer<(object Sender, string EventName)>
    {
        public static readonly EventKeyComparer Instance = new();
        public bool Equals((object Sender, string EventName) x, (object Sender, string EventName) y)
        {
            return ReferenceEquals(x.Sender, y.Sender) && string.Equals(x.EventName, y.EventName, StringComparison.Ordinal);
        }
        public int GetHashCode((object Sender, string EventName) obj)
        {
            return HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Sender), obj.EventName);
        }
    }

    [ThreadStatic]
    private static Dictionary<(object, string), int> _eventDepths;

    public static void SetBackingField(object target, string propName, object value)
    {
        if (target == null) return;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var fld = target.GetType().GetField($"_{char.ToLowerInvariant(propName[0])}{propName.Substring(1)}", flags)
            ?? target.GetType().GetField($"<{propName}>k__BackingField", flags)
            ?? target.GetType().GetField(propName, flags);
        if (fld != null)
        {
            try { fld.SetValue(target, value); return; } catch { }
        }
        var dict = _propertyStore.GetOrCreateValue(target);
        dict[propName] = value;
    }

    public static T GetBackingField<T>(object target, string propName, T defaultValue = default)
    {
        if (target == null) return defaultValue;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var fld = target.GetType().GetField($"_{char.ToLowerInvariant(propName[0])}{propName.Substring(1)}", flags)
            ?? target.GetType().GetField($"<{propName}>k__BackingField", flags)
            ?? target.GetType().GetField(propName, flags);
        if (fld != null)
        {
            try
            {
                object v = fld.GetValue(target);
                if (v is T tv) return tv;
                if (v == null) return defaultValue;
            }
            catch { }
        }
        if (_propertyStore.TryGetValue(target, out var dict) && dict.TryGetValue(propName, out object obj))
        {
            if (obj is T tv) return tv;
        }
        return defaultValue;
    }

    public static void FireEventDirect(object control, string eventName)
    {
        if (control == null) return;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Type curr = control.GetType();
        FieldInfo fld = null;
        while (curr != null && fld == null)
        {
            fld = curr.GetField($"<{eventName}>k__BackingField", flags)
                ?? curr.GetField(eventName, flags)
                ?? curr.GetField($"_{eventName}", flags);
            curr = curr.BaseType;
        }
        if (fld != null)
        {
            var d = fld.GetValue(control) as MulticastDelegate;
            if (d != null)
            {
                FireEvent(d, control, EventArgs.Empty, eventName);
            }
        }
    }

    public static void SetCollectionOwner(object collection, object owner)
    {
        if (collection != null && owner != null)
        {
            _collectionOwners.AddOrUpdate(collection, owner);
        }
    }

    public static object GetCollectionOwner(object collection)
    {
        if (collection != null && _collectionOwners.TryGetValue(collection, out object owner))
        {
            return owner;
        }
        return null;
    }

    public static List<object> GetCollectionList(object collection)
    {
        return _collections.GetOrCreateValue(collection);
    }

    private static void EnsureListViewSubItemsPopulated(object collection)
    {
        if (collection == null) return;
        if (collection.GetType().Name == "ListViewSubItemCollection")
        {
            var list = GetCollectionList(collection);
            if (list.Count == 0)
            {
                object owner = GetCollectionOwner(collection);
                string mainText = owner?.GetType().GetProperty("Text")?.GetValue(owner) as string ?? "";
                var asm = collection.GetType().Assembly;
                var subItemType = asm.GetType("System.Windows.Forms.ListViewItem+ListViewSubItem")
                    ?? asm.GetType("System.Windows.Forms.ListViewItem/ListViewSubItem");
                if (subItemType != null)
                {
                    var mainSubItem = Activator.CreateInstance(subItemType);
                    subItemType.GetProperty("Text")?.SetValue(mainSubItem, mainText);
                    list.Add(mainSubItem);
                    SetChildParent(mainSubItem, collection);
                    SetCollectionOwner(mainSubItem, collection);
                }
            }
        }
    }

    public static object CollectionAdd(object collection, object item)
    {
        if (collection == null) return item;
        EnsureListViewSubItemsPopulated(collection);

        string colTypeName = collection.GetType().Name;

        // Auto-wrap string arguments into concrete collection element types
        if (item is string str)
        {
            var asm = collection.GetType().Assembly;
            if (colTypeName == "ListViewItemCollection")
            {
                var lviType = asm.GetType("System.Windows.Forms.ListViewItem");
                if (lviType != null)
                {
                    var lvi = Activator.CreateInstance(lviType);
                    lviType.GetProperty("Text")?.SetValue(lvi, str);
                    item = lvi;
                }
            }
            else if (colTypeName == "TreeNodeCollection")
            {
                var tnType = asm.GetType("System.Windows.Forms.TreeNode");
                if (tnType != null)
                {
                    var tn = Activator.CreateInstance(tnType);
                    tnType.GetProperty("Text")?.SetValue(tn, str);
                    item = tn;
                }
            }
            else if (colTypeName == "TabPageCollection")
            {
                var tpType = asm.GetType("System.Windows.Forms.TabPage");
                if (tpType != null)
                {
                    var tp = Activator.CreateInstance(tpType);
                    tpType.GetProperty("Text")?.SetValue(tp, str);
                    item = tp;
                }
            }
            else if (colTypeName == "MenuItemCollection")
            {
                var miType = asm.GetType("System.Windows.Forms.MenuItem");
                if (miType != null)
                {
                    var mi = Activator.CreateInstance(miType);
                    miType.GetProperty("Text")?.SetValue(mi, str);
                    item = mi;
                }
            }
            else if (colTypeName == "ToolStripItemCollection")
            {
                var tsmiType = asm.GetType("System.Windows.Forms.ToolStripMenuItem");
                if (tsmiType != null)
                {
                    var tsmi = Activator.CreateInstance(tsmiType);
                    tsmiType.GetProperty("Text")?.SetValue(tsmi, str);
                    item = tsmi;
                }
            }
            else if (colTypeName == "ColumnHeaderCollection")
            {
                var chType = asm.GetType("System.Windows.Forms.ColumnHeader");
                if (chType != null)
                {
                    var ch = Activator.CreateInstance(chType);
                    chType.GetProperty("Text")?.SetValue(ch, str);
                    item = ch;
                }
            }
            else if (colTypeName == "ListViewSubItemCollection")
            {
                var subItemType = asm.GetType("System.Windows.Forms.ListViewItem+ListViewSubItem")
                    ?? asm.GetType("System.Windows.Forms.ListViewItem/ListViewSubItem");
                if (subItemType != null)
                {
                    var subItem = Activator.CreateInstance(subItemType);
                    subItemType.GetProperty("Text")?.SetValue(subItem, str);
                    item = subItem;
                }
            }
        }

        if (collection != null && collection.GetType().Name == "DataGridViewRowCollection")
        {
            if (item is int count)
            {
                int r = DgvRowCollectionAddCount(collection, count);
                return r >= 0 ? CollectionGetItem(collection, r) : null;
            }
            if (item is object[] values)
            {
                int r = DgvRowCollectionAdd(collection, values);
                return r >= 0 ? CollectionGetItem(collection, r) : null;
            }
            if (item != null && item.GetType().Name == "DataGridViewRow")
            {
                DgvRowCollectionAddRow(collection, item);
                return item;
            }
        }
        else if (collection != null && collection.GetType().Name == "DataGridViewColumnCollection")
        {
            if (item != null && item.GetType().Name.StartsWith("DataGridView") && item.GetType().Name.EndsWith("Column"))
            {
                DgvColumnCollectionAddCol(collection, item);
                return item;
            }
        }

        var list = GetCollectionList(collection);
        list.Add(item);
        if (item != null)
        {
            SetChildParent(item, collection);
            SetCollectionOwner(item, collection);
        }
        return item;
    }

    public static object CollectionAddKeyText(object collection, string key, string text)
    {
        var item = CollectionAdd(collection, text);
        if (item != null && key != null)
        {
            var prop = item.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(item, key);
            var fld = item.GetType().GetField("<Name>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            fld?.SetValue(item, key);
        }
        return item;
    }

    public static int CollectionAddEmpty(object collection)
    {
        if (collection == null) return -1;
        if (collection.GetType().Name == "DataGridViewRowCollection")
        {
            return DgvRowCollectionAddEmpty(collection);
        }
        var list = GetCollectionList(collection);
        list.Add(null);
        return list.Count - 1;
    }

    public static void CollectionAddRange(object collection, object items)
    {
        if (collection != null && collection.GetType().Name == "DataGridViewRowCollection")
        {
            DgvRowCollectionAddRange(collection, items);
            return;
        }
        if (collection != null && collection.GetType().Name == "DataGridViewColumnCollection")
        {
            DgvColumnCollectionAddRange(collection, items);
            return;
        }
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
        if (collection == null) return 0;
        EnsureListViewSubItemsPopulated(collection);
        return GetCollectionList(collection).Count;
    }

    public static bool CollectionContains(object collection, object item)
    {
        if (collection == null) return false;
        EnsureListViewSubItemsPopulated(collection);
        return GetCollectionList(collection).Contains(item);
    }

    public static int CollectionIndexOf(object collection, object item)
    {
        if (collection == null) return -1;
        EnsureListViewSubItemsPopulated(collection);
        return GetCollectionList(collection).IndexOf(item);
    }

    public static object CollectionGetItem(object collection, int index)
    {
        if (collection == null) return null;
        EnsureListViewSubItemsPopulated(collection);
        if (collection.GetType().Name == "DataGridViewCellCollection")
        {
            return DgvCellCollectionGetItem(collection, index);
        }
        var list = GetCollectionList(collection);
        if (index >= 0 && index < list.Count) return list[index];
        return null;
    }

    public static object CollectionGetItemByName(object collection, string name)
    {
        if (collection == null || name == null) return null;
        if (collection.GetType().Name == "DataGridViewCellCollection")
        {
            return DgvCellCollectionGetItem(collection, name);
        }

        var list = GetCollectionList(collection);
        foreach (var item in list)
        {
            if (item == null) continue;
            var prop = item.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                ?? item.GetType().GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(item) is string s && string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }
        return null;
    }

    public static void CollectionSetItem(object collection, int index, object value)
    {
        var list = GetCollectionList(collection);
        if (index >= 0 && index < list.Count)
        {
            list[index] = value;
            if (value != null)
            {
                SetChildParent(value, collection);
                SetCollectionOwner(value, collection);
            }
        }
        else if (index >= list.Count)
        {
            while (list.Count < index) list.Add(null);
            list.Add(value);
            if (value != null)
            {
                SetChildParent(value, collection);
                SetCollectionOwner(value, collection);
            }
        }
    }

    public static void CollectionCopyTo(object collection, Array array, int index)
    {
        if (array == null || collection == null) return;
        var list = GetCollectionList(collection);
        for (int i = 0; i < list.Count && (index + i) < array.Length; i++)
        {
            array.SetValue(list[i], index + i);
        }
    }

    public static IEnumerator CollectionGetEnumerator(object collection)
    {
        return GetCollectionList(collection).GetEnumerator();
    }

    public static void CollectionInsert(object collection, int index, object item)
    {
        if (collection != null && collection.GetType().Name == "DataGridViewRowCollection")
        {
            DgvRowCollectionInsert(collection, index, item);
            return;
        }
        var list = GetCollectionList(collection);
        if (index >= 0 && index <= list.Count)
        {
            list.Insert(index, item);
            if (item != null)
            {
                SetChildParent(item, collection);
                SetCollectionOwner(item, collection);
            }
        }
    }

    public static void TableLayoutControlCollectionAdd(object collection, object control, int column, int row)
    {
        CollectionAdd(collection, control);
        if (control != null && collection != null)
        {
            if (_collectionOwners.TryGetValue(collection, out object tlp) && tlp != null)
            {
                try
                {
                    var setCol = tlp.GetType().GetMethod("SetColumn", BindingFlags.Public | BindingFlags.Instance);
                    setCol?.Invoke(tlp, new[] { control, column });
                    var setRow = tlp.GetType().GetMethod("SetRow", BindingFlags.Public | BindingFlags.Instance);
                    setRow?.Invoke(tlp, new[] { control, row });
                }
                catch { }
            }
        }
    }

    // DataGridView helpers
    public static object FindDgv(object obj)
    {
        if (obj == null) return null;
        var seen = new HashSet<object>();
        object cur = obj;
        while (cur != null && seen.Add(cur))
        {
            if (cur.GetType().Name == "DataGridView") return cur;
            object fromStore = GetBackingField<object>(cur, "DataGridView");
            if (fromStore != null && fromStore.GetType().Name == "DataGridView") return fromStore;

            cur = GetCollectionOwner(cur);
        }
        return null;
    }

    public static void DgvRowCreateCells(object row, object dgv, object[] values)
    {
        if (row == null) return;
        dgv ??= FindDgv(row);
        if (dgv == null) return;

        var cells = row.GetType().GetProperty("Cells")?.GetValue(row);
        if (cells == null) return;
        CollectionClear(cells);

        var cols = dgv.GetType().GetProperty("Columns")?.GetValue(dgv);
        var colList = cols != null ? GetCollectionList(cols) : new List<object>();

        var asm = row.GetType().Assembly;
        var defaultCellType = asm.GetType("System.Windows.Forms.DataGridViewTextBoxCell") 
            ?? asm.GetType("System.Windows.Forms.DataGridViewCell");

        for (int i = 0; i < colList.Count; i++)
        {
            var col = colList[i];
            Type cellType = defaultCellType;
            if (col != null)
            {
                var tmpl = col.GetType().GetProperty("CellTemplate")?.GetValue(col);
                if (tmpl != null)
                {
                    cellType = tmpl.GetType();
                }
            }

            var cell = cellType != null ? Activator.CreateInstance(cellType) : null;
            if (cell != null)
            {
                if (values != null && i < values.Length)
                {
                    cell.GetType().GetProperty("Value")?.SetValue(cell, values[i]);
                }
                cell.GetType().GetProperty("OwningRow")?.SetValue(cell, row);
                if (col != null)
                {
                    cell.GetType().GetProperty("OwningColumn")?.SetValue(cell, col);
                }
                SetCollectionOwner(cell, cells);
                var cList = GetCollectionList(cells);
                cList.Add(cell);
            }
        }
    }

    public static int DgvRowCollectionAdd(object rowCollection, object[] values)
    {
        if (rowCollection == null) return -1;
        var asm = rowCollection.GetType().Assembly;
        var rowType = asm.GetType("System.Windows.Forms.DataGridViewRow");
        if (rowType == null) return -1;

        var row = Activator.CreateInstance(rowType);
        object dgv = FindDgv(rowCollection);
        SetCollectionOwner(row, rowCollection);
        var cells = rowType.GetProperty("Cells")?.GetValue(row);
        if (cells != null)
        {
            SetCollectionOwner(cells, row);
        }

        if (dgv != null)
        {
            DgvRowCreateCells(row, dgv, values);
        }

        var list = GetCollectionList(rowCollection);
        list.Add(row);
        return list.Count - 1;
    }

    public static int DgvRowCollectionAddEmpty(object rowCollection)
    {
        return DgvRowCollectionAdd(rowCollection, null);
    }

    public static int DgvRowCollectionAddCount(object rowCollection, int count)
    {
        if (rowCollection == null || count <= 0) return -1;
        int firstIdx = -1;
        for (int i = 0; i < count; i++)
        {
            int idx = DgvRowCollectionAdd(rowCollection, null);
            if (i == 0) firstIdx = idx;
        }
        return firstIdx;
    }

    public static int DgvRowCollectionAddRow(object rowCollection, object row)
    {
        if (rowCollection == null || row == null) return -1;
        object dgv = FindDgv(rowCollection);
        SetCollectionOwner(row, rowCollection);
        var cells = row.GetType().GetProperty("Cells")?.GetValue(row);
        if (cells != null)
        {
            SetCollectionOwner(cells, row);
        }

        if (dgv != null)
        {
            var cols = dgv.GetType().GetProperty("Columns")?.GetValue(dgv);
            int colCount = cols != null ? CollectionCount(cols) : 0;
            int cellCount = cells != null ? CollectionCount(cells) : 0;
            if (cellCount < colCount)
            {
                var colList = GetCollectionList(cols);
                var asm = row.GetType().Assembly;
                var defaultCellType = asm.GetType("System.Windows.Forms.DataGridViewTextBoxCell") 
                    ?? asm.GetType("System.Windows.Forms.DataGridViewCell");
                for (int i = cellCount; i < colCount; i++)
                {
                    var col = colList[i];
                    Type cellType = defaultCellType;
                    if (col != null)
                    {
                        var tmpl = col.GetType().GetProperty("CellTemplate")?.GetValue(col);
                        if (tmpl != null) cellType = tmpl.GetType();
                    }
                    var cell = cellType != null ? Activator.CreateInstance(cellType) : null;
                    if (cell != null)
                    {
                        cell.GetType().GetProperty("OwningRow")?.SetValue(cell, row);
                        if (col != null) cell.GetType().GetProperty("OwningColumn")?.SetValue(cell, col);
                        SetCollectionOwner(cell, cells);
                        GetCollectionList(cells).Add(cell);
                    }
                }
            }
        }

        var list = GetCollectionList(rowCollection);
        list.Add(row);
        return list.Count - 1;
    }

    public static void DgvRowCollectionAddRange(object rowCollection, object rows)
    {
        if (rows is IEnumerable e)
        {
            foreach (var r in e)
            {
                if (r != null)
                {
                    DgvRowCollectionAddRow(rowCollection, r);
                }
            }
        }
    }

    public static void DgvRowCollectionInsert(object rowCollection, int index, object value)
    {
        if (rowCollection == null) return;
        var list = GetCollectionList(rowCollection);
        if (index < 0 || index > list.Count) return;

        if (value is int count)
        {
            for (int i = 0; i < count; i++)
            {
                var asm = rowCollection.GetType().Assembly;
                var rowType = asm.GetType("System.Windows.Forms.DataGridViewRow");
                var r = rowType != null ? Activator.CreateInstance(rowType) : null;
                if (r != null)
                {
                    object dgv = FindDgv(rowCollection);
                    SetCollectionOwner(r, rowCollection);
                    var cells = rowType.GetProperty("Cells")?.GetValue(r);
                    if (cells != null) SetCollectionOwner(cells, r);
                    if (dgv != null) DgvRowCreateCells(r, dgv, null);
                    list.Insert(index + i, r);
                }
            }
            return;
        }

        if (value is object[] values)
        {
            var asm = rowCollection.GetType().Assembly;
            var rowType = asm.GetType("System.Windows.Forms.DataGridViewRow");
            var r = rowType != null ? Activator.CreateInstance(rowType) : null;
            if (r != null)
            {
                object dgv = FindDgv(rowCollection);
                SetCollectionOwner(r, rowCollection);
                var cells = rowType.GetProperty("Cells")?.GetValue(r);
                if (cells != null) SetCollectionOwner(cells, r);
                if (dgv != null) DgvRowCreateCells(r, dgv, values);
                list.Insert(index, r);
            }
            return;
        }

        if (value != null && value.GetType().Name == "DataGridViewRow")
        {
            object dgv = FindDgv(rowCollection);
            SetCollectionOwner(value, rowCollection);
            var cells = value.GetType().GetProperty("Cells")?.GetValue(value);
            if (cells != null) SetCollectionOwner(cells, value);
            if (dgv != null)
            {
                var cols = dgv.GetType().GetProperty("Columns")?.GetValue(dgv);
                int colCount = cols != null ? CollectionCount(cols) : 0;
                int cellCount = cells != null ? CollectionCount(cells) : 0;
                if (cellCount < colCount)
                {
                    var colList = GetCollectionList(cols);
                    var asm = value.GetType().Assembly;
                    var defaultCellType = asm.GetType("System.Windows.Forms.DataGridViewTextBoxCell") 
                        ?? asm.GetType("System.Windows.Forms.DataGridViewCell");
                    for (int i = cellCount; i < colCount; i++)
                    {
                        var col = colList[i];
                        Type cellType = defaultCellType;
                        if (col != null)
                        {
                            var tmpl = col.GetType().GetProperty("CellTemplate")?.GetValue(col);
                            if (tmpl != null) cellType = tmpl.GetType();
                        }
                        var cell = cellType != null ? Activator.CreateInstance(cellType) : null;
                        if (cell != null)
                        {
                            cell.GetType().GetProperty("OwningRow")?.SetValue(cell, value);
                            if (col != null) cell.GetType().GetProperty("OwningColumn")?.SetValue(cell, col);
                            SetCollectionOwner(cell, cells);
                            GetCollectionList(cells).Add(cell);
                        }
                    }
                }
            }
            list.Insert(index, value);
            return;
        }

        list.Insert(index, value);
    }

    public static int DgvColumnCollectionAddCol(object colCollection, object col)
    {
        if (colCollection == null || col == null) return -1;
        SetCollectionOwner(col, colCollection);
        var list = GetCollectionList(colCollection);
        list.Add(col);

        // Expand cells on all existing rows in DGV
        object dgv = FindDgv(colCollection);
        if (dgv != null)
        {
            var rows = dgv.GetType().GetProperty("Rows")?.GetValue(dgv);
            if (rows != null)
            {
                var rowList = GetCollectionList(rows);
                var tmpl = col.GetType().GetProperty("CellTemplate")?.GetValue(col);
                var defaultCellType = col.GetType().Assembly.GetType("System.Windows.Forms.DataGridViewTextBoxCell")
                    ?? col.GetType().Assembly.GetType("System.Windows.Forms.DataGridViewCell");
                Type cellType = tmpl?.GetType() ?? defaultCellType;

                foreach (var row in rowList)
                {
                    if (row == null) continue;
                    var cells = row.GetType().GetProperty("Cells")?.GetValue(row);
                    if (cells == null) continue;
                    var cell = cellType != null ? Activator.CreateInstance(cellType) : null;
                    if (cell != null)
                    {
                        cell.GetType().GetProperty("OwningRow")?.SetValue(cell, row);
                        cell.GetType().GetProperty("OwningColumn")?.SetValue(cell, col);
                        SetCollectionOwner(cell, cells);
                        GetCollectionList(cells).Add(cell);
                    }
                }
            }
        }
        return list.Count - 1;
    }

    public static int DgvColumnCollectionAdd(object colCollection, string name, string headerText)
    {
        if (colCollection == null) return -1;
        var asm = colCollection.GetType().Assembly;
        var colType = asm.GetType("System.Windows.Forms.DataGridViewTextBoxColumn")
            ?? asm.GetType("System.Windows.Forms.DataGridViewColumn");
        if (colType == null) return -1;

        var col = Activator.CreateInstance(colType);
        colType.GetProperty("Name")?.SetValue(col, name);
        colType.GetProperty("HeaderText")?.SetValue(col, headerText);

        return DgvColumnCollectionAddCol(colCollection, col);
    }

    public static void DgvColumnCollectionAddRange(object colCollection, object cols)
    {
        if (cols is IEnumerable e)
        {
            foreach (var col in e)
            {
                if (col != null)
                {
                    DgvColumnCollectionAddCol(colCollection, col);
                }
            }
        }
    }

    public static object DgvCellCollectionGetItem(object cellsCollection, object indexOrName)
    {
        if (cellsCollection == null) return null;
        var list = GetCollectionList(cellsCollection);
        object row = GetCollectionOwner(cellsCollection);
        object dgv = FindDgv(cellsCollection);
        var asm = cellsCollection.GetType().Assembly;
        var defaultCellType = asm.GetType("System.Windows.Forms.DataGridViewTextBoxCell") 
            ?? asm.GetType("System.Windows.Forms.DataGridViewCell");

        if (indexOrName is int idx)
        {
            if (idx >= 0 && idx < list.Count && list[idx] != null)
            {
                return list[idx];
            }

            if (idx >= 0)
            {
                object cols = dgv?.GetType().GetProperty("Columns")?.GetValue(dgv);
                var colList = cols != null ? GetCollectionList(cols) : null;

                while (list.Count <= idx)
                {
                    int curIdx = list.Count;
                    object col = colList != null && curIdx < colList.Count ? colList[curIdx] : null;
                    Type cellType = defaultCellType;
                    if (col != null)
                    {
                        var tmpl = col.GetType().GetProperty("CellTemplate")?.GetValue(col);
                        if (tmpl != null) cellType = tmpl.GetType();
                    }
                    var cell = cellType != null ? Activator.CreateInstance(cellType) : null;
                    if (cell != null)
                    {
                        if (row != null) cell.GetType().GetProperty("OwningRow")?.SetValue(cell, row);
                        if (col != null) cell.GetType().GetProperty("OwningColumn")?.SetValue(cell, col);
                        SetCollectionOwner(cell, cellsCollection);
                    }
                    list.Add(cell);
                }
                if (list[idx] == null)
                {
                    list[idx] = Activator.CreateInstance(defaultCellType);
                }
                return list[idx];
            }
            return null;
        }

        if (indexOrName is string colName)
        {
            if (dgv != null)
            {
                object columns = dgv.GetType().GetProperty("Columns")?.GetValue(dgv);
                if (columns != null)
                {
                    var colList = GetCollectionList(columns);
                    for (int c = 0; c < colList.Count; c++)
                    {
                        var col = colList[c];
                        if (col != null)
                        {
                            var nProp = col.GetType().GetProperty("Name");
                            var hProp = col.GetType().GetProperty("HeaderText");
                            string nameVal = nProp?.GetValue(col) as string;
                            string headerVal = hProp?.GetValue(col) as string;
                            if (string.Equals(nameVal, colName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(headerVal, colName, StringComparison.OrdinalIgnoreCase))
                            {
                                return DgvCellCollectionGetItem(cellsCollection, c);
                            }
                        }
                    }
                }
            }

            foreach (var cell in list)
            {
                if (cell != null)
                {
                    var owningCol = cell.GetType().GetProperty("OwningColumn")?.GetValue(cell);
                    var nameVal = owningCol?.GetType().GetProperty("Name")?.GetValue(owningCol) as string;
                    if (string.Equals(nameVal, colName, StringComparison.OrdinalIgnoreCase))
                    {
                        return cell;
                    }
                }
            }

            return DgvCellCollectionGetItem(cellsCollection, list.Count);
        }

        return null;
    }

    public static void DgvRowCountSet(object dgv, int count)
    {
        if (dgv == null || count < 0) return;
        var rows = dgv.GetType().GetProperty("Rows")?.GetValue(dgv);
        if (rows == null) return;
        var list = GetCollectionList(rows);
        while (list.Count < count)
        {
            DgvRowCollectionAdd(rows, null);
        }
        while (list.Count > count)
        {
            CollectionRemoveAt(rows, list.Count - 1);
        }
    }

    public static void DgvColumnCountSet(object dgv, int count)
    {
        if (dgv == null || count < 0) return;
        var cols = dgv.GetType().GetProperty("Columns")?.GetValue(dgv);
        if (cols == null) return;
        var list = GetCollectionList(cols);
        while (list.Count < count)
        {
            DgvColumnCollectionAdd(cols, null, null);
        }
        while (list.Count > count)
        {
            CollectionRemoveAt(cols, list.Count - 1);
        }
    }

    public static int DgvBandGetIndex(object band)
    {
        if (band == null) return -1;
        object ownerCol = GetCollectionOwner(band);
        if (ownerCol != null)
        {
            int idx = CollectionIndexOf(ownerCol, band);
            if (idx >= 0) return idx;
        }
        object dgv = FindDgv(band);
        if (dgv != null)
        {
            var rows = dgv.GetType().GetProperty("Rows")?.GetValue(dgv);
            if (rows != null)
            {
                int rIdx = CollectionIndexOf(rows, band);
                if (rIdx >= 0) return rIdx;
            }
            var cols = dgv.GetType().GetProperty("Columns")?.GetValue(dgv);
            if (cols != null)
            {
                int cIdx = CollectionIndexOf(cols, band);
                if (cIdx >= 0) return cIdx;
            }
        }
        return GetBackingField<int>(band, "Index", -1);
    }

    public static void DgvBandSetIndex(object band, int index)
    {
        SetBackingField(band, "Index", index);
    }

    public static object DgvGetElementDataGridView(object element)
    {
        return FindDgv(element);
    }

    public static object ToolStripItemCollectionAdd(object collection, string text, object image, EventHandler onClick)
    {
        if (collection == null) return null;
        var asm = collection.GetType().Assembly;
        var tsmiType = asm.GetType("System.Windows.Forms.ToolStripMenuItem");
        if (tsmiType == null) return null;

        var tsmi = Activator.CreateInstance(tsmiType);
        tsmiType.GetProperty("Text")?.SetValue(tsmi, text);
        if (image != null)
        {
            tsmiType.GetProperty("Image")?.SetValue(tsmi, image);
        }
        if (onClick != null)
        {
            var evt = tsmiType.GetEvent("Click");
            evt?.AddEventHandler(tsmi, onClick);
        }

        CollectionAdd(collection, tsmi);
        return tsmi;
    }

    public static object ControlBindingsCollectionAdd(object collection, string propertyName, object dataSource, string dataMember, bool formattingEnabled = false, int updateMode = 0)
    {
        if (collection == null) return null;
        var asm = collection.GetType().Assembly;
        var bindingType = asm.GetType("System.Windows.Forms.Binding");
        object binding = null;
        if (bindingType != null)
        {
            var ctor = bindingType.GetConstructor(new[] { typeof(string), typeof(object), typeof(string) });
            if (ctor != null)
            {
                binding = ctor.Invoke(new object[] { propertyName, dataSource, dataMember });
            }
            else
            {
                binding = Activator.CreateInstance(bindingType);
            }
            var pProp = bindingType.GetProperty("PropertyName");
            if (pProp?.CanWrite == true) pProp.SetValue(binding, propertyName);
            var dProp = bindingType.GetProperty("DataSource");
            if (dProp?.CanWrite == true) dProp.SetValue(binding, dataSource);
        }
        else
        {
            binding = new object();
        }
        CollectionAdd(collection, binding);
        return binding;
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
                if (child.GetType().Name == "TreeNode")
                {
                    SetTreeNodeHierarchy(child, owner);
                    return;
                }
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
            if (child.GetType().Name == "TreeNode")
            {
                SetNodeParent(child, null);
                SetNodeTreeView(child, null);
                return;
            }
            var parentProp = child.GetType().GetProperty("Parent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            parentProp?.SetValue(child, null);
        }
        catch { }
    }

    private static void SetTreeNodeHierarchy(object child, object owner)
    {
        if (child == null || owner == null) return;
        string ownerName = owner.GetType().Name;
        if (ownerName == "TreeView")
        {
            SetNodeParent(child, null);
            SetNodeTreeView(child, owner);
        }
        else if (ownerName == "TreeNode")
        {
            SetNodeParent(child, owner);
            var tv = GetNodeTreeView(owner);
            SetNodeTreeView(child, tv);
        }
    }

    private static void SetNodeParent(object node, object parent)
    {
        if (node == null) return;
        var parentField = node.GetType().GetField("<Parent>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        parentField?.SetValue(node, parent);
    }

    private static void SetNodeTreeView(object node, object treeView)
    {
        if (node == null) return;
        var tvField = node.GetType().GetField("<TreeView>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        tvField?.SetValue(node, treeView);

        // Recursively update all children in node.Nodes
        var nodes = node.GetType().GetProperty("Nodes")?.GetValue(node);
        if (nodes != null)
        {
            var list = GetCollectionList(nodes);
            for (int i = 0; i < list.Count; i++)
            {
                var child = list[i];
                if (child != null)
                {
                    SetNodeTreeView(child, treeView);
                }
            }
        }
    }

    public static object TreeNodeGetTreeView(object node)
    {
        return GetNodeTreeView(node);
    }

    private static object GetNodeTreeView(object node)
    {
        if (node == null) return null;
        var tvField = node.GetType().GetField("<TreeView>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var tv = tvField?.GetValue(node);
        if (tv != null) return tv;

        var cur = GetNodeParent(node);
        while (cur != null)
        {
            tv = cur.GetType().GetField("<TreeView>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(cur);
            if (tv != null) return tv;
            cur = GetNodeParent(cur);
        }
        return null;
    }

    private static object GetNodeParent(object node)
    {
        if (node == null) return null;
        var parentField = node.GetType().GetField("<Parent>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return parentField?.GetValue(node);
    }

    public static int TreeNodeGetIndex(object node)
    {
        if (node == null) return 0;
        var parent = GetNodeParent(node);
        if (parent != null)
        {
            var nodes = parent.GetType().GetProperty("Nodes")?.GetValue(parent);
            if (nodes != null)
            {
                int idx = GetCollectionList(nodes).IndexOf(node);
                if (idx >= 0) return idx;
            }
        }
        var tv = GetNodeTreeView(node);
        if (tv != null)
        {
            var nodes = tv.GetType().GetProperty("Nodes")?.GetValue(tv);
            if (nodes != null)
            {
                int idx = GetCollectionList(nodes).IndexOf(node);
                if (idx >= 0) return idx;
            }
        }
        return 0;
    }

    public static int TreeNodeGetLevel(object node)
    {
        if (node == null) return 0;
        int level = 0;
        var cur = GetNodeParent(node);
        while (cur != null)
        {
            level++;
            cur = GetNodeParent(cur);
        }
        return level;
    }

    public static string TreeNodeGetFullPath(object node)
    {
        if (node == null) return "";
        var tv = GetNodeTreeView(node);
        string sep = "\\";
        if (tv != null)
        {
            var sepProp = tv.GetType().GetProperty("PathSeparator");
            if (sepProp?.GetValue(tv) is string s && !string.IsNullOrEmpty(s))
            {
                sep = s;
            }
        }
        var parts = new List<string>();
        var cur = node;
        while (cur != null)
        {
            var text = cur.GetType().GetProperty("Text")?.GetValue(cur) as string ?? "";
            parts.Add(text);
            cur = GetNodeParent(cur);
        }
        parts.Reverse();
        return string.Join(sep, parts);
    }

    private static List<object> GetNodeSiblingList(object node)
    {
        if (node == null) return null;
        var parent = GetNodeParent(node);
        if (parent != null)
        {
            var nodes = parent.GetType().GetProperty("Nodes")?.GetValue(parent);
            if (nodes != null) return GetCollectionList(nodes);
        }
        var tv = GetNodeTreeView(node);
        if (tv != null)
        {
            var nodes = tv.GetType().GetProperty("Nodes")?.GetValue(tv);
            if (nodes != null) return GetCollectionList(nodes);
        }
        return null;
    }

    public static object TreeNodeGetPrevNode(object node)
    {
        var list = GetNodeSiblingList(node);
        if (list != null)
        {
            int idx = list.IndexOf(node);
            if (idx > 0) return list[idx - 1];
        }
        return null;
    }

    public static object TreeNodeGetNextNode(object node)
    {
        var list = GetNodeSiblingList(node);
        if (list != null)
        {
            int idx = list.IndexOf(node);
            if (idx >= 0 && idx + 1 < list.Count) return list[idx + 1];
        }
        return null;
    }

    public static object TreeNodeGetFirstNode(object node)
    {
        if (node == null) return null;
        var nodes = node.GetType().GetProperty("Nodes")?.GetValue(node);
        if (nodes != null)
        {
            var list = GetCollectionList(nodes);
            if (list.Count > 0) return list[0];
        }
        return null;
    }

    public static object TreeNodeGetLastNode(object node)
    {
        if (node == null) return null;
        var nodes = node.GetType().GetProperty("Nodes")?.GetValue(node);
        if (nodes != null)
        {
            var list = GetCollectionList(nodes);
            if (list.Count > 0) return list[list.Count - 1];
        }
        return null;
    }

    public static object TreeNodeGetPrevVisibleNode(object node)
    {
        var prev = TreeNodeGetPrevNode(node);
        if (prev != null) return prev;
        return GetNodeParent(node);
    }

    public static object TreeNodeGetNextVisibleNode(object node)
    {
        if (node == null) return null;
        bool isExpanded = (bool)(node.GetType().GetProperty("IsExpanded")?.GetValue(node) ?? false);
        if (isExpanded)
        {
            var first = TreeNodeGetFirstNode(node);
            if (first != null) return first;
        }
        var next = TreeNodeGetNextNode(node);
        if (next != null) return next;
        return null;
    }

    public static void TreeNodeExpand(object node)
    {
        if (node == null) return;
        SetNodeBoolField(node, "IsExpanded", true);
    }

    public static void TreeNodeCollapse(object node)
    {
        if (node == null) return;
        SetNodeBoolField(node, "IsExpanded", false);
    }

    public static void TreeNodeToggle(object node)
    {
        if (node == null) return;
        bool cur = (bool)(node.GetType().GetProperty("IsExpanded")?.GetValue(node) ?? false);
        SetNodeBoolField(node, "IsExpanded", !cur);
    }

    private static void SetNodeBoolField(object node, string propName, bool val)
    {
        try
        {
            var prop = node.GetType().GetProperty(propName);
            if (prop?.CanWrite == true)
            {
                prop.SetValue(node, val);
                return;
            }
            var fld = node.GetType().GetField($"<{propName}>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            fld?.SetValue(node, val);
        }
        catch { }
    }

    public static Rectangle TreeNodeGetBounds(object node, Rectangle currentBounds)
    {
        if (!currentBounds.IsEmpty && currentBounds.Width > 0 && currentBounds.Height > 0)
        {
            return currentBounds;
        }
        int index = TreeNodeGetIndex(node);
        return new Rectangle(0, index * 18, 100, 18);
    }

    public static object TreeNodeCollectionFind(object collection, string key, bool searchAllChildren)
    {
        if (collection == null) return null;
        var asm = collection.GetType().Assembly;
        var tnType = asm.GetType("System.Windows.Forms.TreeNode");
        if (tnType == null) return null;

        var matches = new List<object>();
        void Search(object col)
        {
            var list = GetCollectionList(col);
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null) continue;
                var name = item.GetType().GetProperty("Name")?.GetValue(item) as string;
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(item);
                }
                if (searchAllChildren)
                {
                    var children = item.GetType().GetProperty("Nodes")?.GetValue(item);
                    if (children != null) Search(children);
                }
            }
        }
        Search(collection);
        var arr = Array.CreateInstance(tnType, matches.Count);
        for (int i = 0; i < matches.Count; i++) arr.SetValue(matches[i], i);
        return arr;
    }

    public static void TreeViewSetSelectedNode(object treeView, object newSelectedNode)
    {
        if (treeView == null) return;
        var tvType = treeView.GetType();
        var cur = tvType.GetProperty("SelectedNode")?.GetValue(treeView);
        if (ReferenceEquals(cur, newSelectedNode)) return;

        var asm = tvType.Assembly;
        var cancelArgsType = asm.GetType("System.Windows.Forms.TreeViewCancelEventArgs");
        var eventArgsType = asm.GetType("System.Windows.Forms.TreeViewEventArgs");

        var beforeSelect = tvType.GetField("<BeforeSelect>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(treeView) as MulticastDelegate;
        if (beforeSelect != null && cancelArgsType != null)
        {
            object cancelArgs = null;
            var tnType = asm.GetType("System.Windows.Forms.TreeNode");
            var actionType = asm.GetType("System.Windows.Forms.TreeViewAction");
            var ctor = actionType != null ? cancelArgsType.GetConstructor(new[] { tnType, typeof(bool), actionType }) : null;
            if (ctor != null)
            {
                cancelArgs = ctor.Invoke(new object[] { newSelectedNode, false, 0 });
            }
            else
            {
                cancelArgs = Activator.CreateInstance(cancelArgsType);
                cancelArgsType.GetProperty("Node")?.SetValue(cancelArgs, newSelectedNode);
            }
            FireEvent(beforeSelect, treeView, (EventArgs)cancelArgs, "BeforeSelect");
            bool canceled = (bool)(cancelArgsType.GetProperty("Cancel")?.GetValue(cancelArgs) ?? false);
            if (canceled) return;
        }

        var selField = tvType.GetField("<SelectedNode>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        selField?.SetValue(treeView, newSelectedNode);

        var afterSelect = tvType.GetField("<AfterSelect>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(treeView) as MulticastDelegate;
        if (afterSelect != null && eventArgsType != null)
        {
            object eventArgs = null;
            var tnType = asm.GetType("System.Windows.Forms.TreeNode");
            var actionType = asm.GetType("System.Windows.Forms.TreeViewAction");
            var ctor = actionType != null ? eventArgsType.GetConstructor(new[] { tnType, actionType }) : null;
            if (ctor == null) ctor = eventArgsType.GetConstructor(new[] { tnType });
            if (ctor != null && ctor.GetParameters().Length == 2)
            {
                eventArgs = ctor.Invoke(new object[] { newSelectedNode, 0 });
            }
            else if (ctor != null && ctor.GetParameters().Length == 1)
            {
                eventArgs = ctor.Invoke(new object[] { newSelectedNode });
            }
            else
            {
                eventArgs = Activator.CreateInstance(eventArgsType);
                eventArgsType.GetProperty("Node")?.SetValue(eventArgs, newSelectedNode);
            }
            FireEvent(afterSelect, treeView, (EventArgs)eventArgs, "AfterSelect");
        }
    }

    public static object InitPrimaryScreen(object screen)
    {
        if (screen == null) return null;
        try
        {
            var st = screen.GetType();
            var rect = new Rectangle(0, 0, 1920, 1080);

            void SetPropAndField(string name, object val)
            {
                var fld = st.GetField($"<{name}>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                fld?.SetValue(screen, val);
                var prop = st.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(screen, val);
                }
            }

            SetPropAndField("Bounds", rect);
            SetPropAndField("WorkingArea", rect);
            SetPropAndField("Primary", true);
            SetPropAndField("DeviceName", "DISPLAY1");
            SetPropAndField("BitsPerPixel", 32);
        }
        catch { }
        return screen;
    }

    // Equality check for property change detection
    public static bool ValuesEqual(object a, object b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    // Delegate Invocation unwrapping TargetInvocationException
    public static object InvokeDelegate(Delegate d, object[] args)
    {
        if (d == null) return null;
        try
        {
            return d.DynamicInvoke(args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            return null;
        }
    }

    // Event firing with change detection and re-entrancy guard
    public static void FireEvent(MulticastDelegate d, object sender, EventArgs args)
    {
        FireEvent(d, sender, args, "");
    }

    public static void FireEvent(MulticastDelegate d, object sender, EventArgs args, string eventName)
    {
        if (d == null) return;
        args ??= EventArgs.Empty;

        if (sender == null)
        {
            InvokeDelegateDirect(d, sender, args);
            return;
        }

        string name = string.IsNullOrEmpty(eventName) ? (d.GetType().FullName ?? "event") : eventName;
        var depths = _eventDepths ??= new Dictionary<(object, string), int>(EventKeyComparer.Instance);
        var key = (sender, name);

        depths.TryGetValue(key, out int depth);
        if (depth >= 8)
        {
            // Re-entrancy guard: skip firing beyond depth 8 on the same thread
            return;
        }

        depths[key] = depth + 1;
        try
        {
            InvokeDelegateDirect(d, sender, args);
        }
        finally
        {
            if (depths.TryGetValue(key, out int cur))
            {
                if (cur <= 1) depths.Remove(key);
                else depths[key] = cur - 1;
            }
        }
    }

    private static void InvokeDelegateDirect(MulticastDelegate d, object sender, EventArgs args)
    {
        if (d is EventHandler eh)
        {
            eh(sender, args);
            return;
        }

        try
        {
            d.DynamicInvoke(sender, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
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

    // Default font and Graphics support
    private static object _defaultFont;
    public static object GetDefaultFont()
    {
        if (_defaultFont != null) return _defaultFont;
        try
        {
            var fontType = Type.GetType("System.Drawing.Font, LiveSplit.Drawing")
                ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("System.Drawing.Font")).FirstOrDefault(t => t != null);
            if (fontType != null)
            {
                _defaultFont = Activator.CreateInstance(fontType, "Segoe UI", 9f);
            }
        }
        catch { }
        return _defaultFont;
    }

    public static object CreateGraphics(object control)
    {
        try
        {
            var bmpType = Type.GetType("System.Drawing.Bitmap, LiveSplit.Drawing")
                ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("System.Drawing.Bitmap")).FirstOrDefault(t => t != null);
            var gfxType = Type.GetType("System.Drawing.Graphics, LiveSplit.Drawing")
                ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("System.Drawing.Graphics")).FirstOrDefault(t => t != null);
            if (bmpType != null && gfxType != null)
            {
                var bmp = Activator.CreateInstance(bmpType, 1, 1);
                var fromImg = gfxType.GetMethod("FromImage", BindingFlags.Public | BindingFlags.Static);
                return fromImg?.Invoke(null, new[] { bmp });
            }
        }
        catch { }
        return null;
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

    public static void ComboBoxSetDataSource(object control, object dataSource)
    {
        if (control == null) return;
        SetBackingField(control, "DataSource", dataSource);

        var items = control.GetType().GetProperty("Items")?.GetValue(control);
        if (items != null)
        {
            CollectionClear(items);
            if (dataSource is IEnumerable en && !(dataSource is string))
            {
                foreach (var item in en)
                {
                    CollectionAdd(items, item);
                }
            }
        }

        int count = items != null ? CollectionCount(items) : 0;
        int newIdx = count > 0 ? 0 : -1;
        object newSelItem = (count > 0 && items != null) ? CollectionGetItem(items, 0) : null;

        // "DataSource setting populates Items and fires nothing/selects index 0 like WinForms does"
        SetBackingField(control, "SelectedIndex", newIdx);
        SetBackingField(control, "SelectedItem", newSelItem);
    }

    public static void ComboBoxSetSelectedIndex(object control, int newIndex)
    {
        if (control == null) return;
        int oldIndex = GetBackingField<int>(control, "SelectedIndex", -1);
        if (oldIndex == newIndex) return;

        SetBackingField(control, "SelectedIndex", newIndex);

        var items = control.GetType().GetProperty("Items")?.GetValue(control);
        int count = items != null ? CollectionCount(items) : 0;
        object newSelItem = (newIndex >= 0 && newIndex < count && items != null) ? CollectionGetItem(items, newIndex) : null;
        if (newSelItem == null && newIndex >= 0)
        {
            object ds = control.GetType().GetProperty("DataSource")?.GetValue(control);
            if (ds is IList list && newIndex < list.Count) newSelItem = list[newIndex];
        }
        SetBackingField(control, "SelectedItem", newSelItem);

        FireEventDirect(control, "SelectedIndexChanged");
    }

    public static void ComboBoxSetSelectedItem(object control, object newItem)
    {
        if (control == null) return;
        object oldItem = GetBackingField<object>(control, "SelectedItem", null);
        if (ValuesEqual(oldItem, newItem)) return;

        SetBackingField(control, "SelectedItem", newItem);

        // Find new SelectedIndex
        int idx = -1;
        var items = control.GetType().GetProperty("Items")?.GetValue(control);
        if (items != null)
        {
            idx = CollectionIndexOf(items, newItem);
        }
        if (idx < 0)
        {
            object ds = control.GetType().GetProperty("DataSource")?.GetValue(control);
            if (ds is IList list)
            {
                idx = list.IndexOf(newItem);
            }
        }

        SetBackingField(control, "SelectedIndex", idx);
        FireEventDirect(control, "SelectedIndexChanged");
    }

    public static object ComboBoxGetSelectedItem(object control)
    {
        if (control == null) return null;
        object sel = GetBackingField<object>(control, "SelectedItem", null);
        if (sel != null) return sel;

        int idx = GetBackingField<int>(control, "SelectedIndex", -1);
        if (idx >= 0)
        {
            var items = control.GetType().GetProperty("Items")?.GetValue(control);
            if (items != null && idx < CollectionCount(items))
            {
                return CollectionGetItem(items, idx);
            }
            object ds = control.GetType().GetProperty("DataSource")?.GetValue(control);
            if (ds is IList list && idx < list.Count)
            {
                return list[idx];
            }
            if (ds is IEnumerable en)
            {
                int i = 0;
                foreach (var elem in en)
                {
                    if (i == idx) return elem;
                    i++;
                }
            }
        }
        return null;
    }

    public static object ComboBoxGetSelectedValue(object control)
    {
        if (control == null) return null;
        object selItem = ComboBoxGetSelectedItem(control);
        string valMember = control.GetType().GetProperty("ValueMember")?.GetValue(control) as string;
        if (selItem != null && !string.IsNullOrEmpty(valMember))
        {
            var prop = selItem.GetType().GetProperty(valMember);
            if (prop != null) return prop.GetValue(selItem);
        }
        return GetBackingField<object>(control, "SelectedValue", selItem);
    }

    public static void ComboBoxSetSelectedValue(object control, object val)
    {
        if (control == null) return;
        SetBackingField(control, "SelectedValue", val);

        string valMember = control.GetType().GetProperty("ValueMember")?.GetValue(control) as string;
        var items = control.GetType().GetProperty("Items")?.GetValue(control);
        object matchItem = null;

        if (items != null)
        {
            var list = GetCollectionList(items);
            foreach (var item in list)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(valMember))
                {
                    var p = item.GetType().GetProperty(valMember);
                    if (ValuesEqual(p?.GetValue(item), val))
                    {
                        matchItem = item;
                        break;
                    }
                }
                else if (ValuesEqual(item, val))
                {
                    matchItem = item;
                    break;
                }
            }
        }

        if (matchItem != null)
        {
            ComboBoxSetSelectedItem(control, matchItem);
        }
    }

    public static void BindingSourceSetDataSource(object bindingSource, object dataSource)
    {
        if (bindingSource == null) return;
        SetBackingField(bindingSource, "DataSource", dataSource);

        if (dataSource is Type t)
        {
            var blType = typeof(System.ComponentModel.BindingList<>).MakeGenericType(t);
            var bl = Activator.CreateInstance(blType);
            _bindingSourceLists.AddOrUpdate(bindingSource, bl);
        }
        else if (dataSource is IList list)
        {
            _bindingSourceLists.AddOrUpdate(bindingSource, list);
        }
        else if (dataSource is IEnumerable en && !(dataSource is string))
        {
            var bl = new System.ComponentModel.BindingList<object>();
            foreach (var x in en) bl.Add(x);
            _bindingSourceLists.AddOrUpdate(bindingSource, bl);
        }
        else
        {
            var bl = new System.ComponentModel.BindingList<object>();
            _bindingSourceLists.AddOrUpdate(bindingSource, bl);
        }
    }

    public static object BindingSourceGetList(object bindingSource)
    {
        if (bindingSource == null) return new System.ComponentModel.BindingList<object>();
        if (_bindingSourceLists.TryGetValue(bindingSource, out object list) && list != null)
        {
            return list;
        }

        object ds = bindingSource.GetType().GetProperty("DataSource")?.GetValue(bindingSource);
        if (ds is Type t)
        {
            var blType = typeof(System.ComponentModel.BindingList<>).MakeGenericType(t);
            var bl = Activator.CreateInstance(blType);
            _bindingSourceLists.AddOrUpdate(bindingSource, bl);
            return bl;
        }
        if (ds is IList il)
        {
            return il;
        }
        var defaultBl = new System.ComponentModel.BindingList<object>();
        _bindingSourceLists.AddOrUpdate(bindingSource, defaultBl);
        return defaultBl;
    }
}
