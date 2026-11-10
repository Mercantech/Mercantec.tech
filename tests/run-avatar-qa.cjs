// Local full-pack QA. The account/API fixtures remain isolated in Chrome.
const path=require('node:path');
const avatar=process.argv[2]||'raymond';
if(!['raymond','santa','lupp'].includes(avatar))throw new Error('Choose raymond, santa or lupp');
process.env.LUP_QA_AVATAR=avatar;
process.env.LUP_QA_URL=process.argv[3]||process.env.LUP_QA_URL||'http://localhost:3000';
if(process.argv[4])process.env.LUP_QA_OUTPUT=path.resolve(process.argv[4]);
require('./qa-avatar-collection.cjs');
