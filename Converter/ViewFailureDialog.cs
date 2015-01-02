using System;
using System.Windows.Forms;
using Converter.Logic.Schema;

namespace Converter.WinForms
{
    public partial class ViewFailureDialog : Form
    {
        public ViewFailureDialog()
        {
            this.InitializeComponent();
        }

        public ViewSchema View
        {
            get { return _view; }
            set
            {
                _view = value;
                Text = "SQL Error: "+ _view.ViewName;
                txtSQL.Text = _view.ViewSQL;
            }
        }

        public string ViewSQL
        {
            get { return txtSQL.Text; }
        }


        private void btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private ViewSchema _view;
    }
}