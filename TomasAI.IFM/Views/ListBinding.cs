using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Extensions;

namespace TomasAI.IFM.Views
{
    public class ListBinding<TData>
    {
        private string _displayMember;
        private Control _listControl;
        private Func<Task<TData>> _loadData;
        private Func<int, object> _getValue;

        public ListBinding(string displayMember, Control listControl, Func<Task<TData>> loadData, Func<int,object> getValue = null)
        {
            _displayMember = displayMember;
            _listControl = listControl;
            _loadData = loadData;
            _getValue = getValue;
        }

        public ListBinding<TData> Load(Action loadComplete = null)
        {
            _listControl.InvokeAsync(async () => {
                dynamic listControl = _listControl;
                listControl.DisplayMember = _displayMember;
                listControl.DataSource = await _loadData();
                listControl.SelectedIndex = 0;
                if (loadComplete != null)
                    loadComplete();
            });
            return this;
        }

        public string GetValue(string format=null)
        {
            dynamic listControl = _listControl;
            var listIndex = listControl.SelectedIndex;
            return format == null 
                ? $"{_getValue(listIndex)}"
                : _getValue(listIndex).ToString(format);
        }
    }
}
