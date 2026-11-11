// 把源码里写死的旧配色替换成新的主题常量（一次性迁移脚本）
const fs = require('fs');
const path = require('path');

const dir = path.join(__dirname, '..', 'src');
const files = ['Controls.cs', 'Views.cs', 'MainForm.cs', 'Dialogs.cs', 'SetupForm.cs', 'Session.cs'];

const map = [
  ['Color.FromArgb(27, 39, 64)', 'Theme.BubbleBg'],
  ['Color.FromArgb(43, 61, 94)', 'Theme.BubbleBorder'],
  ['Color.FromArgb(58, 68, 92)', 'Theme.Panel3'],
  ['Color.FromArgb(38, 48, 68)', 'Theme.Border'],
  ['Color.FromArgb(196, 210, 230)', 'Theme.CodeText'],
  ['Color.FromArgb(19, 26, 39)', 'Theme.Surface'],
  ['Color.FromArgb(19, 26, 40)', 'Theme.Surface'],
  ['Color.FromArgb(18, 26, 40)', 'Theme.CodeHead'],
  ['Color.FromArgb(93, 104, 128)', 'Theme.Muted2'],
  ['Color.FromArgb(26, 34, 51)', 'Theme.ToastBg'],
  ['Color.FromArgb(60, 78, 110)', 'Theme.ToastBorder'],
  ['Color.FromArgb(195, 208, 230)', 'Theme.TextCol'],
  ['Color.FromArgb(13, 17, 26)', 'Theme.StatusBarBg'],
  ['Color.FromArgb(90, 127, 170, 255)', 'Theme.Link'],
  ['Color.FromArgb(72, 110, 180, 255)', 'Theme.Selection'],
  ['Color.FromArgb(70, 90, 130)', 'Theme.Border'],
  ['Color.FromArgb(70, 85, 120)', 'Theme.Border'],
  ['Color.FromArgb(40, 52, 74)', 'Theme.Border'],
  ['Color.FromArgb(34, 44, 61)', 'Theme.Border'],
  ['Color.FromArgb(30, 39, 55)', 'Theme.BorderSoft'],
  ['Color.FromArgb(28, 38, 56)', 'Theme.InlineCodeBg'],
  ['Color.FromArgb(24, 32, 48)', 'Theme.Panel3'],
  ['Color.FromArgb(22, 30, 45)', 'Theme.Surface'],
  ['Color.FromArgb(20, 27, 41)', 'Theme.Panel2'],
  ['Color.FromArgb(169, 200, 255)', 'Theme.InlineCodeText'],
  ['Color.FromArgb(157, 180, 255)', 'Theme.Accent'],
  ['Color.FromArgb(15, 20, 32)', 'Theme.Panel'],
  ['Color.FromArgb(127, 170, 255)', 'Theme.Link'],
  ['Color.FromArgb(12, 17, 27)', 'Theme.CodeBg'],
  ['Color.FromArgb(107, 154, 255)', 'Theme.Accent2'],
];

let total = 0;
for (const name of files) {
  const p = path.join(dir, name);
  if (!fs.existsSync(p)) continue;
  let s = fs.readFileSync(p, 'utf8');
  let n = 0;
  for (const [from, to] of map) {
    const parts = s.split(from);
    if (parts.length > 1) {
      n += parts.length - 1;
      s = parts.join(to);
    }
  }
  if (n > 0) {
    fs.writeFileSync(p, s);
    console.log(name + ': ' + n + ' 处');
    total += n;
  }
}
console.log('共替换 ' + total + ' 处');
