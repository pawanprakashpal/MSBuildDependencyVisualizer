using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Build.Evaluation;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Color = Microsoft.Msagl.Drawing.Color;

namespace MsbuildDependencyVisualizer.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private readonly GraphViewer _graphViewer = new GraphViewer();
        private readonly Dictionary<string, string> _processedFiles = new Dictionary<string, string>();
        private readonly Dictionary<string, List<string>> _dependencies = new Dictionary<string, List<string>>();

        public MainWindow()
        {
            InitializeComponent();

            _graphViewer.MouseUp += OnMouseCursorChanged;
            _graphViewer.BindToPanel(dockPanel);
#if DEBUG
            txtPath.Text = @"C:\Test\Parent.proj";
#endif
        }

        /// <summary>
        /// Triggered on change of mouse cursor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseCursorChanged(object sender, MsaglMouseEventArgs e)
        {
            foreach (var en in _graphViewer.Entities)
            {
                var node = en as IViewerNode;

                if (node != null)
                {
                    //CLEAR
                    node.Node.Attr.FillColor = Color.Gray;

                    node.OutEdges.ToList().ForEach(x =>
                    {
                        node.Node.Attr.FillColor = Color.LightGreen;
                        x.Edge.SourceNode.Attr.FillColor = Color.LightGreen;
                        x.Edge.Attr.Color = Color.Black;
                    });
                    node.InEdges.ToList().ForEach(x =>
                    {
                        node.Node.Attr.FillColor = Color.LightGreen;
                        x.Edge.TargetNode.Attr.FillColor = Color.LightGreen;
                        x.Edge.Attr.Color = Color.Black;
                    });
                }
            }
            foreach (var en in _graphViewer.Entities)
            {
                var node = en as IViewerNode;

                if (en.MarkedForDragging && node != null)
                {
                    //MARK
                    node.Node.Attr.FillColor = Color.Yellow;

                    node.OutEdges.ToList().ForEach(x =>
                    {
                        x.Edge.TargetNode.Attr.FillColor = Color.PaleVioletRed;
                        x.Edge.Attr.Color = Color.Red;
                    });

                    node.InEdges.ToList().ForEach(x =>
                    {
                        x.Edge.SourceNode.Attr.FillColor = Color.LightBlue;
                        x.Edge.Attr.Color = Color.Blue;
                    });
                }
            }
        }

        /// <summary>
        /// Triggered on click of browse button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            this.ShowDialogWindow();
        }

        /// <summary>
        /// To Show Dialog window
        /// </summary>
        private void ShowDialogWindow()
        {
            var openFileDialog = new OpenFileDialog
                       {
                           CheckFileExists = true,
                           Multiselect = false,
                           RestoreDirectory = true,
                           Filter = "All Files (*.*)|*.*"
                       };
            var showDialog = openFileDialog.ShowDialog();
            if (showDialog.HasValue && showDialog.Value)
                txtPath.Text = openFileDialog.FileName;
        }

        /// <summary>
        /// Triggered on click of start button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnStartClick(object sender, RoutedEventArgs e)
        {
            _processedFiles.Clear();
            _dependencies.Clear();

            if (string.IsNullOrWhiteSpace(txtPath.Text))
            {
                MessageBox.Show(this, "Please select a file", "MSBuild Dependency Visualizer", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var controller = await this.ShowProgressAsync("Please wait...", "Processing files..");
            try
            {
                await TraverseProjects(txtPath.Text, true);
                double i = 1.0;
                var _graph = new Graph();
                foreach (KeyValuePair<string, List<string>> item in _dependencies)
                {
                    double percent = i / _dependencies.Count;
                    controller.SetProgress(percent);
                    controller.SetMessage(string.Format("Processing... {0}", item.Key));
                    await Task.Delay(50);
                    var node = _graph.AddNode(item.Key);
                    node.Attr.FillColor = Color.LightGreen;
                    foreach (var child in item.Value)
                    {
                        var edge = _graph.AddEdge(node.LabelText, child);
                    }
                    i += 1.0;
                }
                _graph.Attr.LayerDirection = LayerDirection.LR;
                _graphViewer.Graph = _graph;
                await controller.CloseAsync();
            }
            catch (Exception exception)
            {
                //await controller.CloseAsync();
                //await this.ShowMessageAsync("Oops! Error occurred", exception.Message);
            }
        }

        /// <summary>
        /// To show Message on click of About 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnAboutClick(object sender, RoutedEventArgs e)
        {
            await this.ShowMessageAsync("MSBuild Dependency Visualizer", "By Utkarsh Shigihalli - CM Team");
        }

        /// <summary>
        /// To traverse the projects
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public async Task TraverseProjects(string rootPath, bool recursive = false)
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            if (_processedFiles.ContainsKey(rootPath)) return;
            var fileName = Path.GetFileName(rootPath);
            var nonImportedProjects = this.GetNonImportedProjects(fileName, rootPath);
            var importedProject = this.GetImportedProjects(nonImportedProjects);
            if (!_dependencies.ContainsKey(fileName))
                _dependencies.Add(fileName, importedProject);
            if (recursive)
            {
                foreach (var import in nonImportedProjects)
                {
                    await TraverseProjects(import.ImportedProject.ProjectFileLocation.File, recursive);
                }
            }
        }

        /// <summary>
        /// To get non-imported projects
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="rootPath"></param>
        /// <returns></returns>
        private List<ResolvedImport> GetNonImportedProjects(string fileName, string rootPath)
        {
            _processedFiles.Add(rootPath, fileName);
            var project = new Project(rootPath);
            return project.Imports.Where(x => !x.IsImported).ToList();
        }

        /// <summary>
        /// To get imported projects
        /// </summary>
        /// <param name="nonImportedProjects"></param>
        /// <returns></returns>
        private List<string> GetImportedProjects(List<ResolvedImport> nonImportedProjects)
        {
            var importedProjects = new List<string>();
            nonImportedProjects.ForEach(project =>
            {
                var importingProject = project.ImportedProject;
                var projectPath = Path.GetFileName(importingProject.ProjectFileLocation.File);
                importedProjects.Add(projectPath);
            });
            return importedProjects;
        }
    }
}