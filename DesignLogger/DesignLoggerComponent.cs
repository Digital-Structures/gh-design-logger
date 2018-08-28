using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

using System.IO;
using System.Windows.Forms;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace DesignLogger
{
    public class DesignLoggerComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>

        /// <summary>

        /// Log file.

        /// </summary>

        protected StreamWriter file = null;

        /// Timer to end session.
        protected Timer timer = null;

        /// Previous month value.
        protected int oldMonth = -1;

        /// Previous day of month value.
        protected int oldDay = -1;

        /// Previous hour of day value.
        protected double oldHour = -1.0;

        public DesignLoggerComponent()
          : base("DesignLogger", "DLog",
              "Logs a design exploration session",
              "DSE", "Research")
        {

            this.DVecs = new List<List<double>>();
            this.OVecs = new List<List<double>>();
            this.DVec = new List<double>();
            this.OVec = new List<double>();
            this.Favorites = new List<List<double>>();
            this.timelist = new List<string>();
            this.count = new int();
            this.favCount = new List<double>();
        }

        public List<List<double>> DVecs;
        public List<List<double>> OVecs;
        public List<List<double>> Favorites;
        public List<double> DVec;
        public List<double> OVec;
        public List<string> timelist;
        public Boolean favorite = false;
        public Boolean End = false;
        public string path;
        public int participant;
        public Boolean first = true;
        public int count;
        public List<double> favCount;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            pManager.AddBooleanParameter("Start", "S", "Start the session", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Participant Number", "PN", "Participant ID number", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Session Length", "SL", "Session length in minutes", GH_ParamAccess.item, 0.5);
            pManager.AddTextParameter("Log Path", "L", "Log file path", GH_ParamAccess.item, (string)null);


            pManager.AddIntegerParameter("Month", "M", "Month [1, 12]", GH_ParamAccess.item, 1);
            pManager.AddIntegerParameter("Day", "D", "Day of month [1, DaysInMonth]", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Hour", "H", "Hour of day [0, 24]", GH_ParamAccess.item, 12.0);

            pManager.AddNumberParameter("Design Vector", "DVec", "Reads design vector", GH_ParamAccess.list);
            pManager.AddNumberParameter("Objective Vector", "OVec", "Reads objective vector", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Favorite", "Fav", "Favorite", GH_ParamAccess.item, false);

            pManager.AddBooleanParameter("End", "E", "End the session", GH_ParamAccess.item, false);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.AddNumberParameter("Favorites", "Favs", "List of 'Favorite' designs", GH_ParamAccess.list);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            if (file == null)
            {

                // See if we are started the component
                bool running = false;
                DA.GetData(0, ref running);
                if (!running) return;


                if (first == true)
                {

                    DA.GetData(1, ref participant);

                    double minutes = 30.0;
                    DA.GetData(2, ref minutes);
                    if (!(minutes > 0.0)) return;

                    DA.GetData(3, ref path);


                    DialogResult result = MessageBox.Show(string.Format("Your {0}-minute session will begin when you press OK.", minutes), "Start Session", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                    if (result != DialogResult.OK) return;

                    timer = new Timer();
                    timer.Interval = (int)(minutes * 60 * 1000);


                    //file = new StreamWriter(path);
                    //log("start");
                    timer.Start();
                    first = false;

                    favCount.Add(0);
                }
            }

            int month = oldMonth;
            DA.GetData(4, ref month);
            int day = oldDay;
            DA.GetData(5, ref day);
            double hour = oldHour;
            DA.GetData(6, ref hour);
            if (month != oldMonth || day != oldDay || hour != oldHour)
            {
                oldMonth = month;
                oldDay = day;
                oldHour = hour;
                log(string.Format("time {0} {1} {2}", month, day, hour));

            }

            DVec.Clear();
            OVec.Clear();

            List<double> DVecTemp = new List<double>();
            List<double> OVecTemp = new List<double>();

            DA.GetDataList<double>(7, DVecTemp);
            DA.GetDataList<double>(8, OVecTemp);

            DA.GetData(9, ref favorite);
            DA.GetData(10, ref End);



            DVecs.Add(DVecTemp);
            OVecs.Add(OVecTemp);
            timelist.Add(DateTime.Now.ToString());

            if (favorite == true)
            {
                Favorites.Add(DVec);
            }


            if (End == true)
            {
                PrintAllSolutions();
                endSession();
            }

            // keep counter and mark favorites

            favCount.Add(0);
            count++;
            if (favorite == true)
            {
                MakeFavorite(DVecTemp);
            }

            //Add favorites output

            DA.SetDataTree(0, ListOfListsToTree<double>(this.Favorites));
        }

        public void PrintAllSolutions()
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(this.path);

            for (int i = 0; i < this.DVecs.Count; i++)
            {
                string design = "";

                List<double> currentDesign = DVecs[i];
                for (int j = 0; j < currentDesign.Count; j++)
                {
                    design = design + currentDesign[j] + ",";
                }

                List<double> currentObj = OVecs[i];
                for (int j = 0; j < currentObj.Count; j++)
                {
                    design = design + currentObj[j] + ",";
                }

                design = design + timelist[i] + ",";
                design = design + favCount[i];

                file.WriteLine(design);
            }
            file.Close();
        }


        public void endSession()

        {

            //Command.UndoRedo -= HandleUndoRedo;

            MessageBox.Show("Please complete the survey", "Time's Up!", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            //System.Diagnostics.Process.Start("https://docs.google.com/forms/d/e/1FAIpQLSdoG65HJc6DJ8ftrhCFpKtwzyqxgfewwfIt7OOYLKp3WUhGUQ/viewform");

        }

        public void MakeFavorite(List<double> Temp)
        {
                favCount[count] = 1;
                Favorites.Add(Temp);            
        }



    protected void log(string message)
        {
            if (file != null)
                file.WriteLine("{0}: {1}", DateTime.Now.ToString(), message);
        }

        static DataTree<T> ListOfListsToTree<T>(List<List<T>> listofLists)
        {
            DataTree<T> tree = new DataTree<T>();
            for (int i = 0; i < listofLists.Count; i++)
            {
                tree.AddRange(listofLists[i], new GH_Path(i));
            }
            return tree;
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("8edb7005-f3b2-4a1e-aa48-b84c2df3e204"); }
        }
    }
}
