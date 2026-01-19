using AntdUI;
using DocumentFormat.OpenXml.Math;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Effects;
using StarResonanceDpsAnalysis.Forms.PopUp;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Plugin.LaunchFunction;
using StarResonanceDpsAnalysis.Properties;
using System.Runtime.InteropServices;
using static System.ComponentModel.Design.ObjectSelectorEditor;

namespace StarResonanceDpsAnalysis.Forms
{
    public partial class SettingsForm : BorderlessForm
    {


        public SettingsForm()
        {
            InitializeComponent();
            SetDefaultFontFromResources();
            FormGui.SetDefaultGUI(this);
            switch1.Checked = AppConfig.ClearPicture == 1;
            select2.SelectedValue = AppConfig.DamageDisplayType;
            languageSelect.SelectedValue = AppConfig.Language;
            LoadDevices();

            ApplyLocalization();


        }
        private void SetDefaultFontFromResources()
        {

            TitleText.Font = AppConfig.TitleFont;
            label1.Font = AppConfig.HeaderFont;
            label2.Font = label3.Font = label4.Font = AppConfig.ContentFont;

            var harmonyOsSansFont = HandledResources.GetHarmonyOS_SansFont(7);
            label6.Font = label7.Font = label8.Font = harmonyOsSansFont;

            InterfaceComboBox.Font = AppConfig.ContentFont;
            select2.Font = label10.Font = label9.Font = input1.Font = input2.Font = input3.Font = input4.Font = input5.Font = inputNumber2.Font = label5.Font = AppConfig.ContentFont;
        }
        private void SettingsForm_Load(object sender, EventArgs e)
        {
            FormGui.SetColorMode(this, AppConfig.IsLight);//设置窗体颜色

            slider1.Value = (int)AppConfig.Transparency;
            inputNumber2.Value = AppConfig.CombatTimeClearDelaySeconds;
        }

        /// <summary>
        /// 鼠标穿透键位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void input1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                input1.Text = string.Empty;
                return;
            }
            input1.Text = e.KeyCode.ToString();
            AppConfig.MouseThroughKey = e.KeyCode;
        }

        /// <summary>
        /// 窗体透明键位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void input2_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                input2.Text = string.Empty;
                return;
            }
            input2.Text = e.KeyCode.ToString();
            AppConfig.FormTransparencyKey = e.KeyCode;
        }

        /// <summary>
        /// 开关键位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void input3_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                input3.Text = string.Empty;
                return;
            }
            input3.Text = e.KeyCode.ToString();
            AppConfig.WindowToggleKey = e.KeyCode;
        }

        /// <summary>
        /// 清空数据键位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void input4_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                input4.Text = string.Empty;
                return;
            }
            input4.Text = e.KeyCode.ToString();
            AppConfig.ClearDataKey = e.KeyCode;
        }

        /// <summary>
        /// 清空历史键位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void input5_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                input5.Text = string.Empty;
                return;
            }
            input5.Text = e.KeyCode.ToString();
            AppConfig.ClearHistoryKey = e.KeyCode;
        }

        private void InterfaceComboBox_SelectedIndexChanged(object sender, IntEventArgs e)
        {
            Console.WriteLine($"InterfaceComboBox_SelectedIndexChanged: {InterfaceComboBox.SelectedIndex}");
            if (AppConfig.NetworkCard == InterfaceComboBox.SelectedIndex)
            {
                Console.WriteLine($"网卡窗口初始化赋值, 或值相等");
                return;
            }

            AppConfig.NetworkCard = InterfaceComboBox.SelectedIndex;

            // 通知MainForm更新网卡设置提示
            try
            {
                // var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                StartupInitializer.RefreshNetworkCardSettingTip();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新MainForm网卡设置提示时出错: {ex.Message}");
            }

            var result = AppMessageBox.ShowMessage("""
                    您已更改网卡设置。

                    请注意，修改网卡后需要重新启动应用程序以使更改生效。
                    是否立刻重新启动应用程序？
                    """, this);
            if (result == DialogResult.OK)
            {
                // 重新启动应用程序
                Application.Restart();
                Environment.Exit(0); // 确保退出当前进程
            }
            else
            {
                AppMessageBox.ShowMessage("您的网卡设置将在下次启动应用时生效。", this);
            }

        }

        private void button5_Click(object sender, EventArgs e)
        {
            // 保存到配置
            AppConfig.Transparency = slider1.Value;
            AppConfig.CombatTimeClearDelaySeconds = (int)inputNumber2.Value;
            AppConfig.DamageDisplayType = select2.SelectedValue.ToString();
            AppConfig.Language = languageSelect.SelectedValue.ToString();

            FormManager.dpsStatistics.ApplyLocalization();
            if (FormManager.moduleCalculationForm != null)
                FormManager.moduleCalculationForm.ApplyLocalization(); //has to be open TODO come back, already applies when constructor
            if (FormManager.skillDetailForm != null)
                FormManager.skillDetailForm.ApplyLocalization();

            EmbeddedSkillConfig.cachedSkillNames.Clear(); // Clear cached skill names to reflect language change

            this.Close();
        }

        private void button4_Click(object sender, EventArgs e)
        {


            this.Close();
        }


        private void TitleText_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                FormManager.ReleaseCapture();
                FormManager.SendMessage(this.Handle, FormManager.WM_NCLBUTTONDOWN, FormManager.HTCAPTION, 0);
            }
        }

        /// <summary>
        /// 检测窗体变动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsForm_ForeColorChanged(object sender, EventArgs e)
        {
            if (Config.IsLight)
            {
                //浅色
                BasicSetupPanel.Back = KeySettingsPanel.Back = CombatSettingsPanel.Back = ColorTranslator.FromHtml("#FFFFFF");
                stackPanel1.Back = ColorTranslator.FromHtml("#EFEFEF");
                //transparencyKnob1.IsDarkMode = false;
            }
            else
            {
                BasicSetupPanel.Back = KeySettingsPanel.Back = CombatSettingsPanel.Back = ColorTranslator.FromHtml("#282828");
                stackPanel1.Back = ColorTranslator.FromHtml("#1E1E1E");
                //transparencyKnob1.IsDarkMode = true;
            }
        }




        private void slider1_ValueChanged(object sender, IntEventArgs e)
        {


            FormManager.FullFormTransparency((double)e.Value / 100);
            //this.BackColor = Color.Transparent;
            //this.TransparencyKey = Color.Transparent;

        }

        private void KeySettingsPanel_Click(object sender, EventArgs e)
        {

        }

        private void switch1_CheckedChanged(object sender, BoolEventArgs e)
        {
            AppConfig.ClearPicture = e.Value ? 1 : 0;
        }
        public void ApplyLocalization()
        {
            // Title and labels
            TitleText.Text = Properties.Strings.Settings_Title;
            label1.Text = Properties.Strings.Settings_Label1;
            label2.Text = Properties.Strings.Settings_Label2;
            label3.Text = Properties.Strings.Settings_Label3;
            label4.Text = Properties.Strings.Settings_Label4;
            label5.Text = Properties.Strings.Settings_Label5;
            label6.Text = Properties.Strings.Settings_Label6;
            label7.Text = Properties.Strings.Settings_Label7;
            label8.Text = Properties.Strings.Settings_Label8;
            label9.Text = Properties.Strings.Settings_Label9;
            label10.Text = Properties.Strings.Settings_Label10;
            label11.Text = Properties.Strings.Settings_Label11;

            // ComboBox/Selects
            InterfaceComboBox.PrefixText = Properties.Strings.Settings_InterfaceComboBox_Prefix;

            select2.Text = Properties.Strings.Settings_Select2;
            select2.Items.Clear();
            select2.Items.AddRange(new object[] { "K", "万 (Wàn) Display (10,000)" });

            // Input fields
            input1.PrefixText = Properties.Strings.Settings_Input1_Prefix;
            input2.PrefixText = Properties.Strings.Settings_Input2_Prefix;
            input3.PrefixText = Properties.Strings.Settings_Input3_Prefix;
            input4.PrefixText = Properties.Strings.Settings_Input4_Prefix;
            input5.PrefixText = Properties.Strings.Settings_Input5_Prefix;
            inputNumber2.PrefixText = Properties.Strings.Settings_InputNumber2_Prefix;
            inputNumber2.SuffixText = Properties.Strings.Settings_InputNumber2_Suffix;
        }

        private void input1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
