using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using McTools.Xrm.Connection;
using System.Collections.Specialized;
using Microsoft.Crm.Sdk.Messages;

namespace CRMSolutionExplorer
{
    public partial class PluginControl : MultipleConnectionsPluginControlBase
    {
        private Settings mySettings;
        private IOrganizationService targetService = null;
        private int uniqueNameIndex = 0;
        private int versionIndex = 0;
        private int solutionIdIndex = 0;
        private int parentSolutionIdIndex = 0;
        private int displayNameIndex = 0;

        private bool isLoading = false;
        private DataGridViewRow selectedSRow = null;
        private DataGridViewRow selectedTRow = null;
        List<DataGridViewRow> selectedRows = new List<DataGridViewRow>();

        public PluginControl()
        {
            InitializeComponent();
        }

        private void MyPluginControl_Load(object sender, EventArgs e)
        {
            // Loads or creates the settings for the plugin
            if (!SettingsManager.Instance.TryLoad(GetType(), out mySettings))
            {
                mySettings = new Settings();

                LogWarning("Settings not found => a new settings file has been created!");
            }
            else
            {
                LogInfo("Settings found and loaded");
            }

            if (ConnectionDetail != null)
            {
                lblSEnvironment.Text = ConnectionDetail.ConnectionName;
                lblSEnvironment.ForeColor = Color.Green;
            }

            // grid fields
            SetGridFields();
        }

        private void tsbClose_Click(object sender, EventArgs e)
        {
            CloseTool();
        }

        private void tsbSample_Click(object sender, EventArgs e)
        {
            // The ExecuteMethod method handles connecting to an
            // organization if XrmToolBox is not yet connected
            ExecuteMethod(GetAccounts);
        }

        private void GetAccounts()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Getting accounts",
                Work = (worker, args) =>
                {
                    args.Result = Service.RetrieveMultiple(new QueryExpression("account")
                    {
                        TopCount = 50
                    });
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState.ToString());
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    var result = args.Result as EntityCollection;
                    if (result != null)
                    {
                        MessageBox.Show($"Found {result.Entities.Count} accounts");
                    }
                }
            });
        }

        /// <summary>
        /// This event occurs when the plugin is closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyPluginControl_OnCloseTool(object sender, EventArgs e)
        {
            // Before leaving, save the settings
            SettingsManager.Instance.Save(GetType(), mySettings);
        }

        /// <summary>
        /// This event occurs when the connection has been updated in XrmToolBox
        /// </summary>
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            if (mySettings != null && detail != null)
            {
                if (!actionName.Equals("AdditionalOrganization"))
                {
                    mySettings.LastUsedOrganizationWebappUrl = detail.WebApplicationUrl;
                    LogInfo("Connection has changed to: {0}", detail.WebApplicationUrl);
                    if (ConnectionDetail != null)
                    {
                        lblSEnvironment.Text = ConnectionDetail.ConnectionName;
                        lblSEnvironment.ForeColor = Color.Green;
                    }
                }
                else
                {
                    ConnectionInfo();
                    return;
                }
            }
        }

        protected override void ConnectionDetailsUpdated(NotifyCollectionChangedEventArgs e)
        {
        }

        private void tsbConnectTarget_Click(object sender, EventArgs e)
        {
            AdditionalConnectionDetails.Clear();
            AddAdditionalOrganization();
        }

        private void ConnectionInfo()
        {
            foreach (var info in AdditionalConnectionDetails)
            {
                lblTEnvironment.Text = info.ConnectionName;
                lblTEnvironment.ForeColor = Color.Green;
                targetService = info.GetCrmServiceClient();

                // then load the target grid
                LoadSolution(targetService, dgvTarget, "Target");
            }
        }

        private void LoadSolution(IOrganizationService service, DataGridView dgv, string msg)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Get {msg} Solutions...",
                Work = (worker, args) =>
                {
                    using (var csm = new CRMSolutionManager(service))
                    {
                        args.Result = csm.RetrieveSolutions(txtSearch.Text, (rbBoth.Checked ? (bool?)null : rbManaged.Checked));
                    }
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState.ToString());
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    var solutions = args.Result as List<Entity>;
                    if (solutions != null)
                    {
                        var loop = 1;
                        this.isLoading = true;
                        dgv.Rows.Clear();
                        dgv.Refresh();
                        foreach (var sol in solutions)
                        {
                            var psId = "";
                            var publisherName = "";
                            if (sol.GetSafe<EntityReference>("parentsolutionid") != null)
                                psId = sol.GetSafe<EntityReference>("parentsolutionid").Id.ToString("B");

                            if (sol.GetSafe<EntityReference>("publisherid") != null)
                                publisherName = sol.GetSafe<EntityReference>("publisherid").Name;

                            var row = new object[] { loop++.ToString(), sol.GetSafe<string>("uniquename"), sol.GetSafe<string>("friendlyname"), sol.GetSafe<string>("version"), sol.GetSafe<DateTime>("installedon")
                                ,(sol.GetSafe<bool>("ismanaged") ? "Managed" : "Unmanaged"), publisherName, sol.GetSafe<string>("description"),
                                (string.IsNullOrWhiteSpace(psId) ? false : true), sol.GetSafe<EntityReference>("createdby").Name,
                                sol.GetSafe<EntityReference>("modifiedby").Name, sol.GetSafe<Guid>("solutionid").ToString("B"), psId,

                            };
                            dgv.Rows.Add(row);
                        }
                        dgv.ClearSelection();
                        this.isLoading = false;
                    }
                }
            });
        }
        private void tsbSLoadSolution_Click(object sender, EventArgs e)
        {
            if(Service == null)
            {
                MessageBox.Show($"Please connect to CRM first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            tsbSClonePatch.Enabled = false;
            tsbSMergePatch.Enabled = false;
            tsbSDeletePatch.Enabled = false;

            if (this.targetService != null && selectedTRow != null)
            {
                LoadSolution(Service, dgvTarget, "Target");
                return;
            }

            if (this.Service != null || selectedSRow != null)
                LoadSolution(this.Service, dgvSource, "Source");
        }

        private void txtSearch_MouseHover(object sender, EventArgs e)
        {
            toolTip.Show("Search only accepts * or ?", (TextBox)sender, 1000);
        }

        #region Data Grid View Methods
        private void SetGridFields()
        {
            dgvSource.ColumnCount = 13;
            dgvSource.Columns[0].Name = "#";
            dgvSource.Columns[0].Width = 50;

            dgvSource.Columns[1].Name = "Name";
            dgvSource.Columns[1].Width = 200;
            this.uniqueNameIndex = 1;

            dgvSource.Columns[2].Name = "Display Name";
            dgvSource.Columns[2].Width = 250;
            this.displayNameIndex = 2;

            dgvSource.Columns[3].Name = "Version";
            dgvSource.Columns[3].Width = 100;
            this.versionIndex = 3;

            dgvSource.Columns[4].Name = "Installed On";
            dgvSource.Columns[4].Width = 150;

            dgvSource.Columns[5].Name = "Package Type";
            dgvSource.Columns[5].Width = 150;

            dgvSource.Columns[6].Name = "Publisher";
            dgvSource.Columns[6].Width = 150;

            dgvSource.Columns[7].Name = "Description";
            dgvSource.Columns[7].Width = 390;

            dgvSource.Columns[8].Name = "Is Patch";
            dgvSource.Columns[8].Width = 100;

            dgvSource.Columns[9].Name = "Created By";
            dgvSource.Columns[9].Width = 150;

            dgvSource.Columns[10].Name = "Modified By";
            dgvSource.Columns[10].Width = 150;

            dgvSource.Columns[11].Name = "Id";
            dgvSource.Columns[11].Visible = false;
            this.solutionIdIndex = 11;

            dgvSource.Columns[12].Name = "ParentSolutionId";
            dgvSource.Columns[12].Visible = false;
            this.parentSolutionIdIndex = 12;

            dgvSource.ReadOnly = true;
            dgvSource.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSource.AllowUserToAddRows = false;

            // target info
            dgvTarget.ColumnCount = 13;
            dgvTarget.Columns[0].Name = "#";
            dgvTarget.Columns[0].Width = 50;

            dgvTarget.Columns[1].Name = "Name";
            dgvTarget.Columns[1].Width = 200;

            dgvTarget.Columns[2].Name = "Display Name";
            dgvTarget.Columns[2].Width = 250;

            dgvTarget.Columns[3].Name = "Version";
            dgvTarget.Columns[3].Width = 100;

            dgvTarget.Columns[4].Name = "Installed On";
            dgvTarget.Columns[4].Width = 150;

            dgvTarget.Columns[5].Name = "Package Type";
            dgvTarget.Columns[5].Width = 150;

            dgvTarget.Columns[6].Name = "Publisher";
            dgvTarget.Columns[6].Width = 150;

            dgvTarget.Columns[7].Name = "Description";
            dgvTarget.Columns[7].Width = 390;

            dgvTarget.Columns[8].Name = "Is Patch";
            dgvTarget.Columns[8].Width = 100;

            dgvTarget.Columns[9].Name = "Created By";
            dgvTarget.Columns[9].Width = 150;

            dgvTarget.Columns[10].Name = "Modified By";
            dgvTarget.Columns[10].Width = 150;

            dgvTarget.Columns[11].Name = "Id";
            dgvTarget.Columns[11].Visible = false;

            dgvTarget.Columns[12].Name = "ParentSolutionId";
            dgvTarget.Columns[12].Visible = false;

            dgvTarget.ReadOnly = true;
            dgvTarget.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvTarget.AllowUserToAddRows = false;
        }

        private void ClearGridSelection(DataGridView dgv, string msg)
        {
            if (dgv == null || dgv.Rows.Count <= 0) return;

            tsbSClonePatch.Enabled = false;
            tsbSMergePatch.Enabled = false;
            tsbSDeletePatch.Enabled = false;
            tsbCopySource2Target.Enabled = false;
            if (msg.Contains("Source"))
            {
                selectedTRow = null;
                dgvTarget.ClearSelection();
            }
            else
            {
                selectedSRow = null;
                dgvSource.ClearSelection();
            }
        }

        private DataGridViewRow UnselectGrid(DataGridView dgv, DataGridViewRow row, int rowIndex, string msg)
        {
            ClearGridSelection(dgv, msg);
            if (row != null && !isLoading && rowIndex >= 0 && row != null && row.Index == rowIndex)
            {
                row = null;
                selectedRows.Clear();
                dgv.ClearSelection();
            }
            else if (rowIndex >= 0)
            {
                row = dgv.Rows[rowIndex];
                if (dgv.SelectedRows.Count == 1) selectedRows.Clear();
                selectedRows.Add(row);
                EnableButton(dgv, row, tsbSClonePatch, tsbSMergePatch, tsbSDeletePatch);
            }
            
            // to avoid confusion and make it simple... if the selected row not matches with the stored rows then clear it...
            if (dgv.SelectedRows.Count != selectedRows.Count)
            {
                row = null;
                selectedRows.Clear();
                dgv.ClearSelection();
            }

            if (this.Service != null && this.targetService != null && this.selectedRows != null && this.selectedRows.Count > 0)
                tsbCopySource2Target.Enabled = true;

            return row;
        }

        private void dgvSource_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            selectedSRow = UnselectGrid(dgvSource, selectedSRow, e.RowIndex, "Source");
        }

        private void dgvTarget_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            selectedTRow = UnselectGrid(dgvTarget, selectedTRow, e.RowIndex, "Target");
        }

        private void EnableButton(DataGridView source, DataGridViewRow row, ToolStripButton btnPatch, ToolStripButton btnMerge, ToolStripButton btnDelete)
        {
            var id = row.Cells[solutionIdIndex].Value;
            if (row.Cells[parentSolutionIdIndex].Value != null && row.Cells[parentSolutionIdIndex].Value.ToString().Length > 0)
            {
                btnMerge.Enabled = false;
                btnDelete.Enabled = true;
                tsbCopySource2Target.Enabled = true;
            }
            else
            {
                var isFound = false;
                foreach (DataGridViewRow gRow in source.Rows)
                {
                    if (gRow.Cells[parentSolutionIdIndex].Value.Equals(id))
                    {
                        isFound = true;
                        break;
                    }
                }

                if (isFound)
                    btnMerge.Enabled = true;
                else
                    btnPatch.Enabled = true;
            }
        }

        private string GetCellValue(DataGridViewRow row, int column)
        {
            return (string)row.Cells[column].Value ?? string.Empty;
        }

        private void dgvSource_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            doDoubleClick(Service, selectedSRow, "Source");
        }

        private void doDoubleClick(IOrganizationService service, DataGridViewRow row, string msg)
        {
            if (row == null) return;

            var usf = new UpdateForm();
            usf.dgvRow = row;
            usf.displayNameIndex = displayNameIndex;
            usf.versionIndex = versionIndex;
            usf.solutionInfo = $"{row.Cells[uniqueNameIndex].Value} - {msg}";
            usf.ShowDialog();

            if (usf.isUpdated)
                UpdateSolution(service, usf.dgvRow, $"Update Solution - {msg}");

            if (msg.Contains("Source"))
                selectedSRow = usf.dgvRow;
            else
                selectedTRow = usf.dgvRow;
        }

        private void UpdateSolution(IOrganizationService service, DataGridViewRow row, string msg)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"{msg}...",
                Work = (worker, args) =>
                {
                    using (var csm = new CRMSolutionManager(service))
                    {
                        var solutionId = Guid.Parse((string)row.Cells[solutionIdIndex].Value);
                        var displayName = (string)row.Cells[displayNameIndex].Value;
                        var version = (string)row.Cells[versionIndex].Value;
                        var result = csm.UpdateSolution(solutionId, displayName, version);

                        args.Result = $"{result}|{displayName}";
                    }
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState.ToString());
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    var results = args.Result.ToStringNullSafe().Split('|');

                    if (Convert.ToBoolean(results[0]))
                        MessageBox.Show($"{msg} completed for [{results[1]}]", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                },
                AsyncArgument = null,
                // Progress information panel size
                MessageWidth = 340,
                MessageHeight = 150
            });

            // reload the grid
            if (msg.Contains("Source"))
                LoadSolution(Service, dgvSource, "Source");
            else
                LoadSolution(targetService, dgvTarget, "Target");
        }

        private void dgvTarget_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            doDoubleClick(targetService, selectedTRow, "Target");
        }
        #endregion

        private void CloneMergePatch(IOrganizationService service, DataGridView dgv, DataGridViewRow row, string msg, bool isMulti = false)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"{msg}...",
                Work = (worker, args) =>
                {
                    using (var csm = new CRMSolutionManager(service))
                    {
                        foreach (DataGridViewRow dgvr in selectedRows)
                        {
                            row = dgvr;
                            var uniqueName = GetCellValue(row, uniqueNameIndex);
                            var displayName = GetCellValue(row, displayNameIndex);
                            var version = GetCellValue(row, versionIndex);
                            var nextVersion = csm.GetNextVersion(version, (msg.Contains("Clone") ? true : false));

                            object result = null;
                            if (msg.Contains("Clone"))
                                result = csm.ClonePatch(uniqueName, displayName, nextVersion);
                            else
                                result = csm.CloneSolution(uniqueName, displayName, nextVersion);

                            args.Result = $"{result}|{displayName}";
                        }
                    }
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState.ToString());
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    var results = args.Result.ToStringNullSafe().Split('|');

                    if (Convert.ToBoolean(results[0]))
                    {
                        if (isMulti)
                            MessageBox.Show($"{msg} completed for {selectedRows.Count} selected patch(es) successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else
                            MessageBox.Show($"{msg} completed for [{results[1]}]", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // reload the grid
                        LoadSolution(Service, dgv, msg);
                    }
                },
                AsyncArgument = null,
                // Progress information panel size
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        private void DeletePatch(IOrganizationService service, DataGridViewRow row, string msg)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"{msg}...",
                Work = (worker, args) =>
                {
                    using (var csm = new CRMSolutionManager(service))
                    {
                        var id = GetCellValue(row, solutionIdIndex);
                        var displayName = GetCellValue(row, displayNameIndex);

                        if (!string.IsNullOrEmpty(id))
                        {
                            var solutionId = new Guid(id);
                            var result = csm.DeleteSolution(solutionId);
                            args.Result = $"{result}|{displayName}";
                        }
                    }
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState.ToString());
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    var results = args.Result.ToStringNullSafe().Split('|');

                    if (Convert.ToBoolean(results[0]))
                    {
                        MessageBox.Show($"{msg} deleted [{results[1]}]", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // reload the grid
                        if (msg.Contains("Source"))
                            LoadSolution(Service, dgvSource, "Source");
                        else
                            LoadSolution(targetService, dgvTarget, "Target");
                    }
                },
                AsyncArgument = null,
                // Progress information panel size
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        private void tsbSClonePath_Click(object sender, EventArgs e)
        {
            if (selectedSRow != null)
            {
                if (Service == null)
                {
                    MessageBox.Show($"Please connect to CRM first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                CloneMergePatch(Service, dgvSource, selectedSRow, "Clone Patch - Source", (dgvSource.SelectedRows.Count > 1));
            }
            else if (selectedTRow != null)
            {
                if (targetService == null) AddAdditionalOrganization();

                if (targetService != null)
                    CloneMergePatch(targetService, dgvTarget, selectedTRow, "Clone Patch - Target", (dgvTarget.SelectedRows.Count > 1));
            }
            else
            {
                MessageBox.Show($"Select Source/Target row's first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void tsbSMergePatch_Click(object sender, EventArgs e)
        {
            if (selectedSRow != null)
            {
                if (Service == null)
                {
                    MessageBox.Show($"Please connect to CRM first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (selectedSRow == null) return;
                CloneMergePatch(Service, dgvSource, selectedSRow, "Merge Patch - Source", (dgvSource.SelectedRows.Count > 1));
            }
            else if (selectedTRow != null)
            {
                if (targetService == null) AddAdditionalOrganization();

                if (targetService != null)
                    CloneMergePatch(targetService, dgvTarget, selectedTRow, "Merge Patch - Target", (dgvTarget.SelectedRows.Count > 1));
            }
            else
            {
                MessageBox.Show($"Select Source/Target row's first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void tsbSDeletePatch_Click(object sender, EventArgs e)
        {
            if (selectedSRow != null)
            {
                var result = MessageBox.Show("Do you really want to delete this patch?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.No) return;

                if (Service == null)
                {
                    MessageBox.Show($"Please connect to CRM first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                DeletePatch(Service, selectedSRow, "Delete Patch - Source");
                tsbSDeletePatch.Enabled = false;
            }
            else if(selectedTRow != null)
            {
                var result = MessageBox.Show("Do you really want to delete this patch?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.No) return;

                if (targetService == null) AddAdditionalOrganization();

                DeletePatch(targetService, selectedTRow, "Delete Patch - Target");
                tsbSDeletePatch.Enabled = false;
            }
            else
            {
                MessageBox.Show($"Select Source/Target row's first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void tsbCopySource2Target_Click(object sender, EventArgs e)
        {
            if (Service == null)
            {
                MessageBox.Show($"Please connect to CRM first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (targetService == null) AddAdditionalOrganization();

            if (this.Service == null || targetService == null) return;

            // move solution source to target
            if (selectedSRow == null && selectedTRow == null)
            {
                MessageBox.Show($"Please select source/target solution to copy!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (selectedSRow != null)
                MoveSolution(Service, targetService, dgvTarget,  selectedSRow, "Source", "Target", (dgvSource.SelectedRows.Count > 1));
            else if (selectedTRow != null)
                MoveSolution(targetService, Service, dgvSource, selectedTRow, "Target", "Source", (dgvTarget.SelectedRows.Count > 1));
        }

        private void MoveSolution(IOrganizationService service, IOrganizationService tService, DataGridView dgv, DataGridViewRow row, string msg, string dMsg, bool isMulti = false)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Solution copying from [{msg}] to [{dMsg}]...",
                Work = (worker, args) =>
                {
                    using (var csm = new CRMSolutionManager(service))
                    {
                        foreach (DataGridViewRow dgvr in selectedRows)
                        {
                            row = dgvr;
                            var uniqueName = (string)row.Cells[uniqueNameIndex].Value;
                            var displayName = (string)row.Cells[displayNameIndex].Value;

                            var fileBytes = csm.ExportSolution(uniqueName, rbManaged.Checked);
                            var result = csm.ImportSolution(tService, fileBytes);

                            args.Result = $"{result}|{displayName}";
                        }
                    }
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState.ToString());
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    var results = args.Result.ToStringNullSafe().Split('|');

                    if (Convert.ToBoolean(results[0]))
                    {
                        if (Convert.ToBoolean(results[0]) && isMulti)
                            MessageBox.Show($"{selectedRows.Count} Solution(s) copied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else if (Convert.ToBoolean(results[0]))
                            MessageBox.Show($"Solution [{results[1]}] export from {msg} and import to {dMsg} completed!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // reload the grid
                        LoadSolution(Service, dgv, dMsg);
                    }
                },
                AsyncArgument = null,
                // Progress information panel size
                MessageWidth = 340,
                MessageHeight = 150
            });
        }

        private void PublishAll(IOrganizationService service, DataGridViewRow row, string msg)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Publish {msg} Solutions...",
                Work = (worker, args) =>
                {
                    using (var csm = new CRMSolutionManager(service))
                    {
                        args.Result = csm.PublishAllSolution();
                    }
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState.ToString());
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            });
        }

        private void tsbPublishAll_Click(object sender, EventArgs e)
        {
            if (Service != null)
            {
                var result = MessageBox.Show($"Do you really want to publish all (Source) [{ConnectionDetail.ConnectionName}]?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.Yes)
                {
                    PublishAll(Service, selectedSRow, "Source");
                }
            }

            if (targetService != null)
            {
                var result = MessageBox.Show($"Do you really want to publish all (Target) [{AdditionalConnectionDetails.FirstOrDefault().ConnectionName}]?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.Yes)
                {
                    PublishAll(targetService, selectedTRow, "Target");
                }
            }
        }

        private void tsbAboutMe_Click(object sender, EventArgs e)
        {
            MessageBox.Show($@"Hi... I'm Murali.{Environment.NewLine}{Environment.NewLine}Working for an Institution called CEWA.{Environment.NewLine}I'm a CRM Developer.{Environment.NewLine}You can reach me at [murali.tk@gmail.com].{Environment.NewLine}Please let me know if you find any issues/needs enhancement.{Environment.NewLine}{Environment.NewLine}Thanks for using this tool!", "About Me", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}