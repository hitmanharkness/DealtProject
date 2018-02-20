using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO.Ports;

namespace AdruinoTest1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();

            try
            {
                SerialPort serialPort1 = new SerialPort("COM6", 9600);
                serialPort1.Open();
                serialPort1.WriteLine("");
                serialPort1.Close();

            }
            catch (Exception ex)
            {

            }
        }
    }
}
