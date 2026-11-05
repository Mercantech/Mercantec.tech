import React, { createContext, useContext, useEffect, useRef, useState } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { BlockNoteSchema, defaultBlockSpecs, type BlockNoteEditor } from '@blocknote/core';
import { filterSuggestionItems, insertOrUpdateBlockForSlashMenu } from '@blocknote/core/extensions';
import { withCollaboration } from '@blocknote/core/yjs';
import { createReactBlockSpec, getDefaultReactSlashMenuItems, SuggestionMenuController, useCreateBlockNote } from '@blocknote/react';
import { BlockNoteView } from '@blocknote/mantine';
import { HocuspocusProvider } from '@hocuspocus/provider';
import * as Y from 'yjs';
import '@blocknote/mantine/style.css';
import './editor.css';
import { danish } from './locale';
import { canonicalFingerprint } from './persistence.mjs';
import { pollUrl, selectedMembers, splitSlides } from './document-utils.mjs';

export interface EditorContext {
  pageId: string;
  holdId: string;
  title: string;
  canEdit: boolean;
  members: { id: string; displayName: string }[];
  polls: { id: string; title: string; state: string }[];
  updatedAtUtc: string;
  userName?: string;
}
type DotnetCallbacks = { invokeMethodAsync: (method: string, ...args: string[]) => Promise<unknown> };
type Status = 'connecting' | 'synced' | 'saving' | 'saved' | 'offline' | 'error';
const Context = createContext<EditorContext | null>(null);
const useData = () => useContext(Context);

const createRoster = createReactBlockSpec({
  type: 'classRoster',
  propSchema: { schemaVersion: { default: 1 }, holdId: { default: '' }, membershipIds: { default: '' } },
  content: 'none',
}, {
  render: ({ block, editor }) => {
    const context = useData();
    if (!context || block.props.holdId !== context.holdId) return <div className="notion-block-note">Elevlisten tilhører et andet hold. Indsæt en ny med /elever.</div>;
    const visible = selectedMembers(context.members, block.props.membershipIds);
    const editable = context.canEdit && editor.isEditable;
    return <section className="notion-class-block" contentEditable={false} aria-label="Elever fra holdet">
      <header><span className="notion-block-symbol" aria-hidden="true">◉</span><div><strong>Elever fra holdet</strong><small>{visible.length} af {context.members.length} elever</small></div></header>
      {editable && <details className="notion-member-picker"><summary>Vælg elever</summary><label><input type="checkbox" checked={!block.props.membershipIds} onChange={event => editor.updateBlock(block, {props:{membershipIds:event.target.checked ? '' : '-'}})} /> Hele holdet</label>
        {context.members.map(member => <label key={member.id}><input type="checkbox" checked={visible.some((item: {id:string}) => item.id === member.id)} onChange={event => {
          const ids = new Set(visible.map((item: {id:string}) => item.id));
          if(event.target.checked) ids.add(member.id); else ids.delete(member.id);
          // '-' explicitly represents no selected students; empty means the whole live roster.
          editor.updateBlock(block, {props:{membershipIds:ids.size ? [...ids].sort().join(',') : '-'}});
        }} /> {member.displayName}</label>)}
      </details>}
      {visible.length ? <table><thead><tr><th scope="col">Elev</th></tr></thead><tbody>{visible.map((member: {id:string;displayName:string}) => <tr key={member.id}><td>{member.displayName}</td></tr>)}</tbody></table> : <p>Ingen elever er valgt.</p>}
      <p className="notion-block-hint">Navne hentes fra holdets elevliste. Listen vises ikke i præsentationen.</p>
    </section>;
  },
  toExternalHTML: () => <p>Elevliste fra holdet</p>,
});
const createPoll = createReactBlockSpec({
  type: 'holdPoll',
  propSchema: { schemaVersion: { default: 1 }, holdId: { default: '' }, pollId: { default: '' } },
  content: 'none',
}, {
  render: ({ block, editor }) => {
    const context = useData();
    const allowed = context && block.props.holdId === context.holdId;
    const poll = allowed ? context.polls.find(item => item.id === block.props.pollId) : undefined;
    const url = poll ? pollUrl(poll.id) : null;
    const presentUrl = poll ? pollUrl(poll.id, true) : null;
    return <section className="notion-poll-block" contentEditable={false} aria-label="Afstemning">
      <header><span className="notion-block-symbol" aria-hidden="true">▥</span><div><strong>{poll?.title || 'Afstemning'}</strong><small>{poll ? ({draft:'Kladde',open:'Åben for svar',closed:'Afsluttet'}[poll.state] || poll.state) : 'Vælg en afstemning fra holdet'}</small></div></header>
      {allowed && context.canEdit && editor.isEditable && <label className="notion-poll-select">Afstemning<select value={poll?.id || ''} onChange={event => editor.updateBlock(block,{props:{pollId:event.target.value}})}><option value="">Vælg afstemning</option>{context.polls.map(item => <option key={item.id} value={item.id}>{item.title}</option>)}</select></label>}
      {url && presentUrl ? <div className="notion-block-actions"><a href={url} target="_blank" rel="noopener noreferrer">Åbn afstemning</a><a href={presentUrl} target="_blank" rel="noopener noreferrer">Vis på tavlen</a></div> : <p className="notion-block-hint">{allowed && !context.polls.length ? <span>Holdet har ingen afstemninger endnu. <a href="/teacher/polls" target="_blank" rel="noopener noreferrer">Opret en afstemning</a>, og opdatér holdkoblingen.</span> : 'Afstemningen er ikke tilgængelig på dette hold.'}</p>}
    </section>;
  },
  toExternalHTML: () => <p>Afstemning fra holdet</p>,
});
const createSlideBreak = createReactBlockSpec({type:'slideBreak',propSchema:{schemaVersion:{default:1}},content:'none'}, {
  render: () => <div className="notion-slide-break" contentEditable={false}><span>Slidegrænse</span></div>,
  toExternalHTML: () => <hr />,
});
const createLearningGoal = createReactBlockSpec({type:'learningGoal',propSchema:{schemaVersion:{default:1}},content:'inline'}, {
  render: ({contentRef}) => <section className="notion-learning-goal"><strong contentEditable={false}>Læringsmål</strong><div ref={contentRef} /></section>,
  toExternalHTML: ({contentRef}) => <section><strong>Læringsmål</strong><div ref={contentRef} /></section>,
});
const schema = BlockNoteSchema.create({ blockSpecs: {...defaultBlockSpecs, classRoster:createRoster(),holdPoll:createPoll(),slideBreak:createSlideBreak(),learningGoal:createLearningGoal()} });
type Editor = BlockNoteEditor<typeof schema.blockSchema, typeof schema.inlineContentSchema, typeof schema.styleSchema>;

interface Session {
  root: Root; context: EditorContext; callbacks?: DotnetCallbacks; doc: Y.Doc; provider?: HocuspocusProvider;
  editor?: Editor; ready: boolean; inSync: boolean; connected: boolean; dirty: boolean; generation: number;
  destroyed: boolean; status: Status; detail: string; presenting: boolean; slides: ReturnType<typeof splitSlides>;
  theme: 'light' | 'dark'; render: () => void; removeListeners: () => void;
}
const sessions = new WeakMap<HTMLElement, Session>();
function notify(session: Session, status: Status, detail: string) {
  if(session.destroyed) return;
  session.status=status; session.detail=detail; session.render();
  void session.callbacks?.invokeMethodAsync('OnNotionStatus',status,detail).catch(() => {});
}
function canEdit(session: Session) { return session.context.canEdit && session.connected && session.inSync && !session.presenting; }
function readTheme(): 'light' | 'dark' {
  const value = document.documentElement.dataset.theme || document.body.dataset.theme || '';
  if(['dark','hacker'].includes(value.toLowerCase())) return 'dark';
  if(['light','christmas','jul'].includes(value.toLowerCase())) return 'light';
  const scheme=getComputedStyle(document.documentElement).colorScheme.trim().replace(/^only\s+/, '');
  if(scheme==='light' || scheme==='dark')return scheme;
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}
function openPresentation(session: Session) {
  if(!session.editor) return;
  session.slides=splitSlides(session.editor.document);
  session.presenting=true; session.render();
}

function ConnectedEditor({session}:{session:Session}) {
  const editor=useCreateBlockNote(withCollaboration({
    schema, dictionary:danish,
    collaboration:{fragment:session.doc.getXmlFragment('document-store'),provider:{awareness:session.provider?.awareness ?? undefined},user:{name:session.context.userName || 'Underviser',color:'#2452e8'},showCursorLabels:'activity'},
  }),[session]);
  session.editor=editor;
  useEffect(() => () => { session.editor=undefined; },[session]);
  const enabled=canEdit(session);
  const insert=(type:'classRoster'|'holdPoll'|'slideBreak'|'learningGoal') => {
    if(!enabled) return;
    editor.focus();
    const cursor=editor.getTextCursorPosition().block;
    const add=(block: Parameters<Editor['insertBlocks']>[0][number])=>{
      if(cursor.content!==undefined) insertOrUpdateBlockForSlashMenu(editor,block);
      else {const inserted=editor.insertBlocks([block,{type:'paragraph'}],cursor,'after');editor.setTextCursorPosition(inserted[1],'end');}
    };
    if(type==='classRoster') add({type,props:{holdId:session.context.holdId}});
    else if(type==='holdPoll') add({type,props:{holdId:session.context.holdId}});
    else if(type==='learningGoal') add({type,content:'Efter aktiviteten kan du…'});
    else add({type});
  };
  const customItems=[
    {title:'Elever fra holdet',subtext:'Indsæt en levende elevliste',aliases:['elever','klasse','hold','students'],group:'Undervisning',icon:<span aria-hidden="true">◉</span>,onItemClick:()=>insert('classRoster')},
    {title:'Afstemning',subtext:'Kobl en afstemning fra holdet på siden',aliases:['afstemning','poll','stemning'],group:'Undervisning',icon:<span aria-hidden="true">▥</span>,onItemClick:()=>insert('holdPoll')},
    {title:'Slidegrænse',subtext:'Start næste slide i præsentationen',aliases:['slide','slides','præsentation'],group:'Undervisning',icon:<span aria-hidden="true">▱</span>,onItemClick:()=>insert('slideBreak')},
    {title:'Læringsmål',subtext:'Fremhæv målet for aktiviteten',aliases:['læringsmål','mål','learning'],group:'Undervisning',icon:<span aria-hidden="true">◎</span>,onItemClick:()=>insert('learningGoal')},
  ];
  return <>
    <div className="notion-editor-tools" role="toolbar" aria-label="Dokumentværktøjer">
      <button type="button" onClick={()=>editor.undo()} disabled={!enabled} title="Fortryd (Ctrl+Z)">↶ <span>Fortryd</span></button>
      <button type="button" onClick={()=>editor.redo()} disabled={!enabled} title="Gendan (Ctrl+Shift+Z)">↷ <span>Gendan</span></button>
      <span className="notion-tool-divider" />
      <button type="button" onClick={()=>insert('classRoster')} disabled={!enabled}>Elever</button>
      <button type="button" onClick={()=>insert('holdPoll')} disabled={!enabled}>Afstemning</button>
      <button type="button" onClick={()=>insert('slideBreak')} disabled={!enabled}>Slidegrænse</button>
      <button type="button" className="notion-present-button" onClick={()=>openPresentation(session)}>Præsenter</button>
    </div>
    {!session.inSync && <p className="notion-connection-note" role="status">Redigering fortsætter, når forbindelsen er gendannet. Afventende ændringer bevares i denne fane.</p>}
    <BlockNoteView editor={editor} editable={enabled} theme={session.theme} slashMenu={false}>
      <SuggestionMenuController triggerCharacter="/" getItems={async query=>filterSuggestionItems([...customItems,...getDefaultReactSlashMenuItems(editor)],query)} />
    </BlockNoteView>
    <footer className="notion-editor-footnote"><span>Skriv <kbd>/</kbd> for indhold og undervisningsblokke.</span><a href="/notion-editor/THIRD-PARTY-NOTICES.txt" target="_blank" rel="noopener noreferrer">Open source</a></footer>
  </>;
}
function SlideContent({blocks,theme}:{blocks:ReturnType<typeof splitSlides>[number];theme:'light'|'dark'}) {
  const editor=useCreateBlockNote({schema,dictionary:danish,initialContent:blocks as NonNullable<Parameters<typeof useCreateBlockNote>[0]>['initialContent']});
  return <BlockNoteView editor={editor} editable={false} theme={theme} formattingToolbar={false} sideMenu={false} slashMenu={false} />;
}
function SlideDeck({session}:{session:Session}) {
  const [index,setIndex]=useState(0);
  const dialog=useRef<HTMLDialogElement>(null);
  const total=session.slides.length;
  const close=()=>{session.presenting=false;session.render();};
  useEffect(()=>{
    const element=dialog.current!;
    element.showModal();
    const previous=document.body.style.overflow; document.body.style.overflow='hidden';
    return ()=>{element.close();document.body.style.overflow=previous;};
  },[]);
  return <dialog ref={dialog} className="notion-slide-deck" aria-label="Præsentation" onCancel={event=>{event.preventDefault();close();}} onKeyDown={event=>{
    if(event.key==='ArrowRight' || event.key==='PageDown'){event.preventDefault();setIndex(value=>Math.min(total-1,value+1));}
    if(event.key==='ArrowLeft' || event.key==='PageUp'){event.preventDefault();setIndex(value=>Math.max(0,value-1));}
  }}>
    <header className="notion-slide-header"><strong>{session.context.title}</strong><span>{index+1} / {total}</span><button type="button" autoFocus onClick={close}>Luk præsentation</button></header>
    <article className="notion-slide-paper"><SlideContent key={index} blocks={session.slides[index]} theme={session.theme} /></article>
    <nav className="notion-slide-controls" aria-label="Skift slide"><button type="button" disabled={index===0} onClick={()=>setIndex(index-1)}>‹ Forrige</button><span>Brug piletasterne til at skifte slide</span><button type="button" disabled={index===total-1} onClick={()=>setIndex(index+1)}>Næste ›</button></nav>
  </dialog>;
}
class EditorBoundary extends React.Component<{session:Session;children:React.ReactNode},{failed:boolean}> {
  state={failed:false};
  static getDerivedStateFromError(){return {failed:true};}
  componentDidCatch(){notify(this.props.session,'error','Editoren kunne ikke åbne dokumentet. Genindlæs siden, eller kontakt en administrator.');}
  render(){return this.state.failed ? <p className="notion-editor-error" role="alert">Editoren kunne ikke åbne dokumentet. Genindlæs siden for at prøve igen.</p> : this.props.children;}
}
function App({session}:{session:Session}) {
  return <Context.Provider value={session.context}><div className="notion-blocknote-host" data-notion-theme={session.theme}>
    {session.status==='error' && <p className="notion-editor-persistence-error" role="alert">{session.detail}</p>}
    <EditorBoundary session={session}>{session.ready ? <ConnectedEditor session={session} /> : <div className="notion-editor-loading" role="status">Forbinder til dokumentet…</div>}{session.presenting && <SlideDeck session={session}/>}</EditorBoundary>
  </div></Context.Provider>;
}

export async function mount(element:HTMLElement,context:EditorContext,accessToken:string,dotnetCallbacks?:DotnetCallbacks):Promise<void> {
  destroy(element);
  if(!context.pageId || !context.holdId || !accessToken) throw new Error('Dokument, hold og login er påkrævet.');
  const session:Session={root:createRoot(element),context,callbacks:dotnetCallbacks,doc:new Y.Doc(),ready:false,inSync:false,connected:false,dirty:false,generation:0,destroyed:false,status:'connecting',detail:'Forbinder til dokumentet…',presenting:false,slides:[],theme:readTheme(),render:()=>{},removeListeners:()=>{}};
  sessions.set(element,session);
  session.render=()=>{if(!session.destroyed)session.root.render(<App session={session}/>);};
  const onBeforeUnload=(event:BeforeUnloadEvent)=>{if(session.dirty){event.preventDefault();event.returnValue='';}};
  window.addEventListener('beforeunload',onBeforeUnload);
  const onTheme=()=>{session.theme=readTheme();session.render();};
  const observer=new MutationObserver(onTheme);observer.observe(document.documentElement,{attributes:true,attributeFilter:['data-theme','style','class']});observer.observe(document.body,{attributes:true,attributeFilter:['data-theme','style','class']});
  const media=window.matchMedia('(prefers-color-scheme: dark)');media.addEventListener('change',onTheme);
  session.removeListeners=()=>{window.removeEventListener('beforeunload',onBeforeUnload);observer.disconnect();media.removeEventListener('change',onTheme);};
  session.doc.on('update',(_update:Uint8Array,origin:unknown)=>{
    if(origin!==session.provider && session.ready && !session.destroyed){session.dirty=true;session.generation++;notify(session,session.connected?'saving':'offline',session.connected?'Gemmer i skyen…':'Forbindelsen er afbrudt. Ændringer afventer lagring.');}
  });
  session.provider=new HocuspocusProvider({
    url:(location.protocol==='https:'?'wss:':'ws:')+'//'+location.host+'/collaboration',name:'notion:'+context.pageId,document:session.doc,token:async()=>{if(!session.callbacks)return accessToken;const token=await session.callbacks.invokeMethodAsync('GetNotionToken');if(typeof token!=='string' || !token)throw new Error('Log ind igen for at åbne dokumentet.');return token;},
    onStatus:({status})=>{session.connected=status==='connected';if(!session.connected){session.inSync=false;notify(session,status==='connecting'?'connecting':'offline',status==='connecting'?'Forbinder til dokumentet…':'Forbindelsen er afbrudt. Redigering er sat på pause.');}},
    onSynced:({state})=>{if(!state)return;session.ready=true;session.inSync=true;session.connected=true;notify(session,session.dirty?'saving':'synced',session.dirty?'Gemmer afventende ændringer…':'Forbundet til dokumentet');},
    onAuthenticationFailed:()=>{session.connected=false;session.inSync=false;notify(session,'error','Du har ikke længere adgang, eller dit login er udløbet. Log ind igen.');},
    onStateless:({payload})=>{void (async()=>{
      let message:{type?:string;fingerprintHash?:string};try{message=JSON.parse(payload);}catch{return;}
      if(session.destroyed)return;
      if(message.type==='persistenceError'){session.dirty=true;notify(session,'error','Dokumentet kunne ikke gemmes i skyen. Ændringerne bevares i denne fane, mens serveren prøver igen.');return;}
      if(message.type!=='persisted' || !message.fingerprintHash)return;
      const generation=session.generation;
      const fingerprint=canonicalFingerprint(session.doc);
      const digest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(fingerprint));
      const hash=Array.from(new Uint8Array(digest)).map(value=>value.toString(16).padStart(2,'0')).join('');
      if(generation===session.generation && fingerprint===canonicalFingerprint(session.doc) && hash===message.fingerprintHash && session.connected && session.inSync){session.dirty=false;notify(session,'saved','Gemt i skyen');}
    })().catch(()=>{if(session.dirty)notify(session,'saving','Afventer bekræftelse på lagring…');});},
  });
  notify(session,'connecting','Forbinder til dokumentet…');
}
export function destroy(element:HTMLElement):void {
  const session=sessions.get(element);if(!session)return;
  session.destroyed=true;session.removeListeners();session.root.unmount();session.provider?.destroy();session.doc.destroy();sessions.delete(element);
}
export function setContext(element:HTMLElement,context:EditorContext):void {
  const session=sessions.get(element);if(!session)return;
  if(context.pageId!==session.context.pageId || context.holdId!==session.context.holdId)throw new Error('Dokumentskift kræver en ny editor.');
  session.context=context;session.render();
}
export function setPresentation(element:HTMLElement,presenting:boolean):void {
  const session=sessions.get(element);if(!session)return;
  if(presenting)openPresentation(session);else{session.presenting=false;session.render();}
}
export function hasUnsavedChanges(element:HTMLElement):boolean {return sessions.get(element)?.dirty ?? false;}
