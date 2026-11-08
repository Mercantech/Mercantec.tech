const fs=require('node:fs/promises'),path=require('node:path');
const os=require('node:os');
function dependency(name){try{return require(name);}catch{return require(path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules',name));}}
const {chromium}=dependency('playwright');
const root=path.resolve(__dirname,'../..'),folder=path.join(root,'audio');
(async()=>{
  const manifest=JSON.parse(await fs.readFile(path.join(folder,'audio-manifest.json'),'utf8'));
  const browser=await chromium.launch({executablePath:process.env.BIP_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true,args:['--autoplay-policy=no-user-gesture-required']});
  try{
    const page=await browser.newPage();await page.goto('about:blank');
    const results=[];
    for(const item of [...manifest.sfx,...manifest.phrases]){
      const bytes=await fs.readFile(path.join(folder,item.file));
      const result=await page.evaluate(async({encoded,id,spoken})=>{
        const bytes=Uint8Array.from(atob(encoded),char=>char.charCodeAt(0));
        const ctx=new AudioContext({sampleRate:44100});
        const decoded=await ctx.decodeAudioData(bytes.buffer.slice(0));
        let playback='not-requested';
        if(spoken){
          const url=URL.createObjectURL(new Blob([bytes],{type:'audio/wav'}));
          const player=new Audio(url);player.volume=.2;
          await player.play();playback=player.paused?'failed':'started';player.pause();player.src='';URL.revokeObjectURL(url);
        }
        const report={id,durationSeconds:decoded.duration,sampleRate:decoded.sampleRate,channels:decoded.numberOfChannels,playback};
        await ctx.close();return report;
      },{encoded:bytes.toString('base64'),id:item.id,spoken:item.language==='da-DK'});
      if(Math.abs(result.durationSeconds-item.durationSeconds)>.001||result.sampleRate!==44100||result.channels!==1||(item.language==='da-DK'&&result.playback!=='started'))throw Error('Browser audio mismatch: '+item.id);
      results.push(result);
    }
    const report={passed:true,decoded:results.length,spokenPlaybackStarted:results.filter(x=>x.playback==='started').length,results,scope:'Actual Chromium AudioContext decoding for all files and successful HTMLAudioElement playback requests for five speech files. This is not a subjective human listening or speech-recognition test.'};
    await fs.writeFile(path.join(folder,'browser-audio-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({passed:true,decoded:results.length,spokenPlaybackStarted:5}));
  }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
