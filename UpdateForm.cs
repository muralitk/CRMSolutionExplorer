using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRMSolutionExplorer
{
    public partial class UpdateForm : Form
    {
        public DataGridViewRow dgvRow { get; set; }
        public int displayNameIndex { get; set; }
        public int versionIndex { get; set; }
        public string solutionInfo { get; set; }

        public bool isUpdated { get; set; } = false;
        public UpdateForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            isUpdated = true;
            dgvRow.Cells[displayNameIndex].Value = txtDisplayName.Text;
            dgvRow.Cells[versionIndex].Value = txtVersion.Text;
            button1_Click(sender, e);
        }

        private void UpdateForm_Load(object sender, EventArgs e)
        {
            lblSolution.Text = solutionInfo;
            txtDisplayName.Text = (string)dgvRow.Cells[displayNameIndex].Value;
            txtVersion.Text = (string)dgvRow.Cells[versionIndex].Value;
        }
    }
}
