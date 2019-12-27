using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using FLSIGCTLLib;
using FlSigCaptLib;
using NLog;
using System.Configuration;


namespace TestSigCapt
{

    public partial class MaubankSigCapt : Form
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        List<String> tempSig = new List<String>();
        List<String> tempID = new List<String>();

        private String prevCIF = "";

        private String prevACC = "";

        private int count = 0;

        public MaubankSigCapt()
        {
            InitializeComponent();
            this.txtAccNum.Click += new EventHandler(txtAccNum_Click);
            this.txtCIF.Click += new EventHandler(txtCIF_Click);
            imgID.SizeMode = PictureBoxSizeMode.Zoom;

        }

        String fileName;

        private void btnSign_Click(object sender, EventArgs e)
        {
            if (this.txtAccNum.Text.Trim().Length > 0 && this.txtCIF.Text.Trim().Length > 0)
            {
                SigCtl sigCtl = new SigCtl();
                sigCtl.Licence = @ConfigurationManager.AppSettings["licenceFile"];
                String pathString = @ConfigurationManager.AppSettings["tempSignaturePath"];

                System.IO.Directory.CreateDirectory(pathString);

                DynamicCapture dc = new DynamicCapture();
                DynamicCaptureResult res = dc.Capture(sigCtl, this.txtCIF.Text.Trim() + "-" +this.txtAccNum.Text.Trim(), "AUTHORIZED SIGNATORY", null, null);
                if (res == DynamicCaptureResult.DynCaptOK)
                {
                    print("Signature captured successfully");
                    SigObj sigObj = (SigObj)sigCtl.Signature;
                    sigObj.set_ExtraData("CIF", this.txtCIF.Text.Trim());
                    //print("Hash" + sigObj.GetHashCode().ToString());
                    fileName = pathString + @"\signature-" + this.txtCIF.Text.Trim() + ".tif";

                    this.tempSig.Add(fileName);
                    try
                    {
                        sigObj.RenderBitmap(fileName, Convert.ToInt32(ConfigurationManager.AppSettings["imageSizeX"]), Convert.ToInt32(ConfigurationManager.AppSettings["imageSizeY"]), "image/tiff", 0.5f, 0xff0000, 0xffffff, 10.0f, 10.0f, RBFlags.RenderOutputFilename | RBFlags.RenderColor32BPP | RBFlags.RenderEncodeData);


                        using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                        {
                            sigImage.Image = System.Drawing.Image.FromStream(fs);
                            fs.Close();
                        }


                    }
                    catch (Exception ex)
                    {

                        logger.Error(ex, "error while generating signature for customer with CIF" + txtCIF.Text);
                        MessageBox.Show(ex.Message);
                    }

                }
                else
                {
                    print("Signature capture error res=" + (int)res + "  ( " + res + " )");
                    logger.Error("Error while generating signature for customer with CIF" + txtCIF.Text + ". Error code: " + res);
                    switch (res)
                    {
                        case DynamicCaptureResult.DynCaptCancel: print("Signature capture cancelled"); break;
                        case DynamicCaptureResult.DynCaptError: print("No capture service available"); break;
                        case DynamicCaptureResult.DynCaptPadError: print("Signing device error"); break;
                        default: print("Unexpected error code "); break;
                    }
                }
            }
            else
            {
                MessageBox.Show("Please provide a CIF or Account Number before proceeding.");
            }
        }
        private void print(string txt)
        {
            txtDisplay.Text += txt + "\r\n";
            txtDisplay.SelectionStart = txtDisplay.TextLength;
            txtDisplay.ScrollToCaret();
        }

        private void button1_Click(object sender, EventArgs e)
        {
        this.imgID.Dispose();
            this.sigImage.Dispose();
            try
            {
                foreach (String str in this.tempID)
                {
                    String[] fileNames = str.Split('\\');
                    int len = fileNames.Length;
                    DateTime now = DateTime.Now;
                    if (!Directory.Exists(@ConfigurationManager.AppSettings["NICArchive"] + now.ToString("yyyyMMdd")))
                    {
                        Directory.CreateDirectory(@ConfigurationManager.AppSettings["NICArchive"] + now.ToString("yyyyMMdd"));
                    }
                    String dest = @ConfigurationManager.AppSettings["NICArchive"] +now.ToString("yyyyMMdd") +'\\'+  now.ToString("HHmmssfff") +  '-' + fileNames[len - 1];
                    File.Move(str,dest );
                    //File.Delete(str);
                }

                foreach (String str in this.tempSig)
                {
                     //MessageBox.Show("deleting " + str);
                    File.Delete(str);
                }

                this.Close();

            }
            catch (Exception exc)
            {
                logger.Error(exc, "error while deleting temporary files for customer with CIF" + txtAccNum.Text);
                MessageBox.Show("Some temporary files have not been deleted. Please check with your administrator to complete clean up process. Error is " + exc.Message);
            }
        }

        private void txtCIF_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsLetter(e.KeyChar) ||
                   char.IsSymbol(e.KeyChar) ||
                   char.IsWhiteSpace(e.KeyChar) ||
                   char.IsPunctuation(e.KeyChar))
                e.Handled = true;

        }

        private bool mergeImages()
        {
            if (sigImage.Image != null && imgID.Image != null)
            {
                try
                {
                    int outputImageWidth = sigImage.Image.Width > imgID.Image.Width ? sigImage.Image.Width : imgID.Image.Width;

                    int outputImageHeight = sigImage.Image.Height + imgID.Image.Height + 1;

                    Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);


                    using (Graphics graphics = Graphics.FromImage(outputImage))
                    {
                        graphics.Clear(Color.White);
                        graphics.DrawImage(imgID.Image, new Rectangle(new Point(), imgID.Image.Size),
                            new Rectangle(new Point(), imgID.Image.Size), GraphicsUnit.Pixel);
                        graphics.DrawImage(sigImage.Image, new Rectangle(new Point(0, imgID.Image.Height + 10), sigImage.Image.Size),
                            new Rectangle(new Point(), sigImage.Image.Size), GraphicsUnit.Pixel);

                    }


                    string hash;
                    ImageConverter converter = new ImageConverter();

                    using (SHA512CryptoServiceProvider sha1 = new SHA512CryptoServiceProvider())
                    {
                        hash = Convert.ToBase64String(sha1.ComputeHash((byte[])converter.ConvertTo(outputImage, typeof(byte[]))));
                        print("HASH: " + hash);
                    }
                    String fileName = @ConfigurationManager.AppSettings["destinationFolder"] + this.txtCIF.Text.Trim() + "-" + this.txtAccNum.Text.Trim();
                    if (this.fileExists() || (this.txtCIF.Text.Trim().Equals(this.prevCIF) && this.txtAccNum.Text.Trim().Equals(this.prevACC)))
                    {
                        this.count++;
                        fileName += "-" + count ;
                    }
                    else
                    {
                        this.count = 0;
                        this.prevACC = this.txtAccNum.Text.Trim();
                        this.prevCIF = this.txtCIF.Text.Trim();
                        
                       
                    }
                    this.saveHash(hash, fileName);
                    fileName += ".tif";
                    outputImage.Save(fileName, ImageFormat.Tiff);

                    


                    return true;
                }
                catch (Exception e)
                {
                    logger.Error(e, "error while saving signature for customer with CIF" + txtCIF.Text);
                    MessageBox.Show("Error: Could not save signature. Original error: " + e.Message);
                    return false;
                }
            }
            else
            {
                MessageBox.Show("Please select a valid NIC and capture customer signature before proceeding.");
                return false;
            }
        }

        private Boolean fileExists()
        {
            Boolean exists = false;
            if (File.Exists(@ConfigurationManager.AppSettings["destinationFolder"] + this.txtCIF.Text.Trim() + "-" + this.txtAccNum.Text.Trim() + ".tif"))
            {
                exists = true;
                this.count = 0;
                this.prevACC = this.txtAccNum.Text.Trim();
                this.prevCIF = this.txtCIF.Text.Trim();
            }
            if(File.Exists(@ConfigurationManager.AppSettings["destinationFolder"] + this.txtCIF.Text.Trim() + "-" + this.txtAccNum.Text.Trim() +  "-1.tif")){
                exists =true;
                this.count = 1;
                this.prevACC = this.txtAccNum.Text.Trim();
                this.prevCIF = this.txtCIF.Text.Trim();
            }
            if(File.Exists(@ConfigurationManager.AppSettings["destinationFolder"] + this.txtCIF.Text.Trim() + "-" + this.txtAccNum.Text.Trim() +  "-2.tif")){
                exists =true;
                this.count = 2;
                this.prevACC = this.txtAccNum.Text.Trim();
                this.prevCIF = this.txtCIF.Text.Trim();
            }
            if(File.Exists(@ConfigurationManager.AppSettings["destinationFolder"] + this.txtCIF.Text.Trim() + "-" + this.txtAccNum.Text.Trim() +  "-3.tif")) {
                exists =true;
                this.count = 3;
                this.prevACC = this.txtAccNum.Text.Trim();
                this.prevCIF = this.txtCIF.Text.Trim();
            }
            if(File.Exists(@ConfigurationManager.AppSettings["destinationFolder"] + this.txtCIF.Text.Trim() + "-" + this.txtAccNum.Text.Trim() +  "-4.tif")){
                exists = true;
                this.count = 4;
                this.prevACC = this.txtAccNum.Text.Trim();
                this.prevCIF = this.txtCIF.Text.Trim();
            }
            return exists;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            String pathString = @ConfigurationManager.AppSettings["tempSignaturePath"] + this.txtCIF.Text.Trim();
            System.IO.Directory.Delete(pathString, false);

        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {

            OpenFileDialog theDialog = new OpenFileDialog();
            theDialog.Title = "Please select a proof of ID for the customer";
            theDialog.Filter = "TIFF files|*.tif";
            theDialog.InitialDirectory = @ConfigurationManager.AppSettings["NICSource"];
            if (theDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {

                    imgID.Load(theDialog.FileName);

                    this.tempID.Add(theDialog.FileName);

                }
                catch (Exception ex)
                {
                    logger.Error(ex, "error while loading signature image for customer with CIF" + txtCIF.Text);
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
            }
            theDialog.RestoreDirectory = true;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (this.mergeImages())
            {

                // this.Close();


                //tempID.Add(this.sigImage.ImageLocation);


                this.txtAccNum.Clear();
                this.txtCIF.Clear();
                this.sigImage.Image = null;
                this.imgID.Image = null;

                //this.sigImage.Dispose();
                //this.imgID.Dispose();
                this.sigImage.Refresh();
                this.imgID.Refresh();


                MessageBox.Show("Signature Successfully saved.");



            };
        }


        private void txtCIF_Click(object sender, EventArgs e)
        {
            this.txtCIF.Select(0, 0);
        }

        private void txtAccNum_Click(object sender, EventArgs e)
        {
            this.txtAccNum.Select(0, 0);
        }


        private bool saveHash(String hash, String fileName)
        {
            bool result = false;
            try
            {
                string path = fileName + ".txt";

                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine(hash);
                }



            }
            catch (Exception e)
            {
                logger.Error(e, "error while saving hash for customer with CIF" + txtCIF.Text);
            }

            return result;
        }

        private void MaubankSigCapt_Load(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void maskedTextBox1_MaskInputRejected(object sender, MaskInputRejectedEventArgs e)
        {

        }

      
     

    }
}
