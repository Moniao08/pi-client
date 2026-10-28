// 把文本文件转成 CRLF 行尾（cmd.exe 的 ^ 续行要求 CRLF）
const fs = require('fs');
const path = require('path');
const CRLF = String.fromCharCode(13) + String.fromCharCode(10);
const LF = String.fromCharCode(10);

const files = process.argv.slice(2);
for (const f of files) {
  const p = path.resolve(f);
  let s = fs.readFileSync(p, 'utf8');
  s = s.split(CRLF).join(LF).split(LF).join(CRLF);
  fs.writeFileSync(p, s, 'utf8');
  console.log('crlf:', p);
}
