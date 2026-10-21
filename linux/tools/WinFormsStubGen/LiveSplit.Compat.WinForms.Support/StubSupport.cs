using System;
using System.Collections;
using System.Collections.Generic;
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
        }
        return item;
    }

    public static int CollectionAddEmpty(object collection)
    {
        if (collection == null) return -1;
        if (collection.GetType().Name == "DataGridViewRowCollection")
        {
            return DgvRowCollectionAdd(collection, null);
        }
        var list = GetCollectionList(collection);
        list.Add(null);
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
    public static int DgvRowCollectionAdd(object rowCollection, object[] values)
    {
        if (rowCollection == null) return -1;
        var asm = rowCollection.GetType().Assembly;
        var rowType = asm.GetType("System.Windows.Forms.DataGridViewRow");
        if (rowType == null) return -1;

        var row = Activator.CreateInstance(rowType);
        var cells = rowType.GetProperty("Cells")?.GetValue(row);

        // Pre-populate cells matching columns if DGV is known
        object dgv = GetCollectionOwner(rowCollection);
        if (dgv != null)
        {
            SetCollectionOwner(row, dgv);
        }
        object cols = dgv?.GetType().GetProperty("Columns")?.GetValue(dgv);
        int colCount = cols != null ? CollectionCount(cols) : 0;

        var cellType = asm.GetType("System.Windows.Forms.DataGridViewTextBoxCell") 
            ?? asm.GetType("System.Windows.Forms.DataGridViewCell");

        int targetCells = Math.Max(colCount, values != null ? values.Length : 0);
        if (cells != null && cellType != null)
        {
            for (int i = 0; i < targetCells; i++)
            {
                var cell = Activator.CreateInstance(cellType);
                if (values != null && i < values.Length)
                {
                    cellType.GetProperty("Value")?.SetValue(cell, values[i]);
                }
                CollectionAdd(cells, cell);
            }
        }

        CollectionAdd(rowCollection, row);
        return CollectionCount(rowCollection) - 1;
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

        CollectionAdd(colCollection, col);
        return CollectionCount(colCollection) - 1;
    }

    public static object DgvCellCollectionGetItem(object cellsCollection, object indexOrName)
    {
        if (cellsCollection == null) return null;
        var list = GetCollectionList(cellsCollection);

        if (indexOrName is int idx)
        {
            if (idx >= 0 && idx < list.Count) return list[idx];
            // Lazily expand cells if requested index is beyond count
            if (idx >= 0)
            {
                var asm = cellsCollection.GetType().Assembly;
                var cellType = asm.GetType("System.Windows.Forms.DataGridViewTextBoxCell") 
                    ?? asm.GetType("System.Windows.Forms.DataGridViewCell");
                while (list.Count <= idx)
                {
                    var cell = cellType != null ? Activator.CreateInstance(cellType) : null;
                    CollectionAdd(cellsCollection, cell);
                }
                return list[idx];
            }
            return null;
        }

        if (indexOrName is string colName)
        {
            // Find in parent DGV columns
            object row = GetCollectionOwner(cellsCollection);
            object dgv = row != null ? GetCollectionOwner(row) : null;
            while (dgv != null && dgv.GetType().Name != "DataGridView")
            {
                dgv = GetCollectionOwner(dgv);
            }
            if (dgv == null && row != null)
            {
                var dgvProp = row.GetType().GetProperty("DataGridView", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                dgv = dgvProp?.GetValue(row);
            }

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
                            if (string.Equals(nProp?.GetValue(col) as string, colName, StringComparison.OrdinalIgnoreCase))
                            {
                                return DgvCellCollectionGetItem(cellsCollection, c);
                            }
                        }
                    }
                }
            }

            // Fallback: check cells directly
            foreach (var cell in list)
            {
                if (cell != null)
                {
                    var nameProp = cell.GetType().GetProperty("OwningColumn")?.GetValue(cell)?.GetType().GetProperty("Name");
                    if (string.Equals(nameProp?.GetValue(cell) as string, colName, StringComparison.OrdinalIgnoreCase))
                    {
                        return cell;
                    }
                }
            }

            // If still not found, create a cell and return it
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
        // Fallback to checking DataGridView.Columns or Rows
        var dgvProp = band.GetType().GetProperty("DataGridView", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var dgv = dgvProp?.GetValue(band);
        if (dgv != null)
        {
            var cols = dgv.GetType().GetProperty("Columns")?.GetValue(dgv);
            if (cols != null)
            {
                int cIdx = CollectionIndexOf(cols, band);
                if (cIdx >= 0) return cIdx;
            }
            var rows = dgv.GetType().GetProperty("Rows")?.GetValue(dgv);
            if (rows != null)
            {
                int rIdx = CollectionIndexOf(rows, band);
                if (rIdx >= 0) return rIdx;
            }
        }
        return 0;
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
}
