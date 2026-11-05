import React, { createContext, useContext, useEffect, useRef, useState } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { BlockNoteSchema, defaultBlockSpecs, defaultInlineContentSpecs, type BlockNoteEditor } from '@blocknote/core';
import { filterSuggestionItems, insertOrUpdateBlockForSlashMenu } from '@blocknote/core/extensions';
import { withCollaboration } from '@blocknote/core/yjs';
import { createReactBlockSpec, createReactInlineContentSpec, FormattingToolbar, FormattingToolbarController, getDefaultReactSlashMenuItems, SideMenu, SideMenuController, SuggestionMenuController, useCreateBlockNote } from '@blocknote/react';
import { BlockNoteView } from '@blocknote/mantine';
import { HocuspocusProvider } from '@hocuspocus/provider';
import * as Y from 'yjs';
import '@blocknote/mantine/style.css';
import './editor.css';
import { danish } from './locale';
import { canonicalFingerprint, publishedDocumentFromBase64 } from './persistence.mjs';
import { pollUrl, selectedMembers, splitSlides, pageUrl, studentUrl, collectPublicationAssetIds } from './document-utils.mjs';
import { createMermaidDiagram, DEFAULT_MERMAID_SOURCE } from './mermaid-block';
import { createCallout } from './callout-block';
import { NotionDragHandleMenu, turnIntoSelectItems } from './turn-into';

export interface EditorContext {
  pageId: string;
  holdId: string;
  title: string;
  canEdit: boolean;
  members: { id: string; displayName: string }[];
  polls: { id: string; title: string; state: string }[];
  updatedAtUtc: string;
  userName?: string;
  pages?: {id:string;title:string;parentPageId?:string|null}[];
  groups?: {id:string;name:string;members:{id:string;displayName:string}[]}[];
  publishedStateBase64?:string|null;
  publishedAtUtc?:string|null;
}
type DotnetCallbacks = { invokeMethodAsync: (method: string, ...args: string[]) => Promise<unknown> };
type Status = 'connecting' | 'synced' | 'saving' | 'saved' | 'offline' | 'error';
const Context = createContext<EditorContext | null>(null);
const useData = () => useContext(Context);
const Runtime = createContext<Session | null>(null);

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
      {visible.length ? <table><thead><tr><th scope="col">Elev</th><th scope="col">Grupper</th></tr></thead><tbody>{visible.map((member: {id:string;displayName:string}) => <tr key={member.id}><td><a href={studentUrl(context.holdId,member.id) ?? undefined}>{member.displayName}</a></td><td>{context.groups?.filter(group=>group.members.some(item=>item.id===member.id)).map(group=>group.name).join(', ') || '—'}</td></tr>)}</tbody></table> : <p>Ingen elever er valgt.</p>}
      {editable && <GroupButton ids={visible.map((member:{id:string})=>member.id)} />}
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
      {url && presentUrl ? <div className="notion-block-actions"><a href={url} target="_blank" rel="noopener noreferrer">Åbn afstemning</a>{context?.canEdit && <a href={presentUrl} target="_blank" rel="noopener noreferrer">Vis på tavlen</a>}</div> : <p className="notion-block-hint">{allowed && !context.polls.length ? <span>Holdet har ingen afstemninger endnu. <a href="/teacher/polls" target="_blank" rel="noopener noreferrer">Opret en afstemning</a>, og opdatér holdkoblingen.</span> : 'Afstemningen er ikke tilgængelig på dette hold.'}</p>}
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
function GroupButton({ids}:{ids:string[]}) {
  const session=useContext(Runtime);
  return <button type="button" className="notion-group-create" disabled={!ids.length || !session?.callbacks} onClick={()=>{
    void session?.callbacks?.invokeMethodAsync('OnNotionCreateGroup',JSON.stringify(ids)).catch(()=>notify(session,'error','Gruppen kunne ikke oprettes. Prøv igen.'));
  }}>Opret gruppe af {ids.length} valgte elever</button>;
}

const pageMention=createReactInlineContentSpec({type:'pageMention',propSchema:{pageId:{default:''}},content:'none'},{
  render:({inlineContent})=>{const context=useData();const page=context?.pages?.find(item=>item.id===inlineContent.props.pageId);return page ? <a className="notion-mention" href={pageUrl(page.id,context?.canEdit===false) ?? undefined} contentEditable={false}>▤ {page.title}</a> : <span className="notion-unavailable" contentEditable={false}>Side er ikke tilgængelig</span>;},
});
const studentMention=createReactInlineContentSpec({type:'studentMention',propSchema:{holdId:{default:''},membershipId:{default:''}},content:'none'},{
  render:({inlineContent})=>{const context=useData();const member=context?.holdId===inlineContent.props.holdId ? context.members.find(item=>item.id===inlineContent.props.membershipId) : undefined;return member && context ? <a className="notion-mention" href={studentUrl(context.holdId,member.id) ?? undefined} contentEditable={false}>@{member.displayName}</a> : <span className="notion-unavailable" contentEditable={false}>Elev er ikke tilgængelig</span>;},
});
const createPageLink=createReactBlockSpec({type:'pageLink',propSchema:{schemaVersion:{default:1},pageId:{default:''}},content:'none'},{
  render:({block,editor})=>{const context=useData();const page=context?.pages?.find(item=>item.id===block.props.pageId);return <section className="notion-link-block" contentEditable={false} aria-label="Link til side">
    {context?.canEdit && editor.isEditable && <label>Link til side<select value={page?.id || ''} onChange={event=>editor.updateBlock(block,{props:{pageId:event.target.value}})}><option value="">Vælg side eller underside</option>{context.pages?.map(item=><option key={item.id} value={item.id}>{item.title}</option>)}</select></label>}
    {page ? <a href={pageUrl(page.id,context?.canEdit===false) ?? undefined}>▤ {page.title}</a> : <p>Siden er ikke tilgængelig.</p>}
  </section>;},
  toExternalHTML:()=> <p>Link til undervisningsside</p>,
});
const createGroup=createReactBlockSpec({type:'holdGroup',propSchema:{schemaVersion:{default:1},holdId:{default:''},groupId:{default:''}},content:'none'},{
  render:({block,editor})=>{const context=useData();const allowed=context?.holdId===block.props.holdId;const group=allowed ? context.groups?.find(item=>item.id===block.props.groupId) : undefined;return <section className="notion-group-block" contentEditable={false} aria-label="Holdgruppe">
    {allowed && context.canEdit && editor.isEditable && <label>Gruppe<select value={group?.id || ''} onChange={event=>editor.updateBlock(block,{props:{groupId:event.target.value}})}><option value="">Vælg gemt gruppe</option>{context.groups?.map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
    {group && context ? <><strong>{group.name}</strong><ul>{group.members.map(member=><li key={member.id}><a href={studentUrl(context.holdId,member.id) ?? undefined}>{member.displayName}</a></li>)}</ul></> : <p>Vælg en gruppe fra holdet. Grupper oprettes fra elevlisten eller under holdets grupper.</p>}
  </section>;},
  toExternalHTML:()=> <p>Gruppe fra holdet</p>,
});

interface AssetAccess {url:string;downloadUrl:string;expiresAtUtc:string;originalName:string;contentType:string;kind:string;sizeBytes:number;}
async function validToken(session:Session) {
  if(!session.callbacks)return session.accessToken;
  const token=await session.callbacks.invokeMethodAsync('GetNotionToken');
  if(typeof token!=='string' || !token)throw new Error('Log ind igen for at åbne filen.');
  return token;
}
function MediaBlock({assetId,pageId,kind,onUpload,editable}:{assetId:string;pageId:string;kind:string;onUpload:(id:string,kind:string)=>void;editable:boolean}) {
  const session=useContext(Runtime);
  const [access,setAccess]=useState<AssetAccess|null>(null);
  const [error,setError]=useState('');
  const [uploading,setUploading]=useState(false);
  const [playbackError,setPlaybackError]=useState(false);
  const media=useRef<HTMLMediaElement|null>(null);
  const resumeAt=useRef(0);
  const resumePlaying=useRef(false);
  const [retry,setRetry]=useState(0);
  useEffect(()=>{
    if(!assetId || !session)return;
    setAccess(null);setError('');setPlaybackError(false);
    const controller=new AbortController();let timer:ReturnType<typeof setTimeout>|undefined;
    const load=async()=>{
      try {
        const token=await validToken(session);
        const response=await fetch('/api/notion/assets/'+encodeURIComponent(assetId)+'/access',{headers:{Authorization:'Bearer '+token},signal:controller.signal,cache:'no-store'});
        if(!response.ok)throw new Error(response.status===404?'Filen er ikke tilgængelig i denne udgave.':'Filen kunne ikke åbnes. Prøv igen.');
        const result=await response.json() as AssetAccess;
        if(controller.signal.aborted)return;
        if(media.current){resumeAt.current=media.current.currentTime;resumePlaying.current=!media.current.paused;}
        setAccess(result);setError('');
        const until=Date.parse(result.expiresAtUtc)-Date.now()-30000;
        timer=setTimeout(()=>void load(),Math.max(15000,Math.min(until,240000)));
      }catch(ex){if(!controller.signal.aborted){setAccess(null);setError(ex instanceof Error?ex.message:'Filen kunne ikke åbnes.');}}
    };
    void load();
    return()=>{controller.abort();if(timer)clearTimeout(timer);};
  },[assetId,retry,session]);
  const upload=async(file:File)=>{
    if(!session || !editable || uploading)return;
    if(file.size>100*1024*1024){setError('Filen må højst være 100 MiB.');return;}
    setUploading(true);setError('');session.pendingUploads++;notify(session,'saving','Uploader fil…');
    try {
      const token=await validToken(session);const body=new FormData();body.append('file',file);
      const response=await fetch('/api/notion/pages/'+encodeURIComponent(session.context.pageId)+'/assets',{method:'POST',headers:{Authorization:'Bearer '+token},body});
      if(!response.ok)throw new Error(response.status===413?'Filen må højst være 100 MiB.':response.status===400?'Filtypen understøttes ikke, eller filen er ugyldig.':'Upload kunne ikke gennemføres. Prøv igen.');
      const result=await response.json();if(session.destroyed)return;
      onUpload(result.id,result.kind);
    }catch(ex){if(!session.destroyed)setError(ex instanceof Error?ex.message:'Upload kunne ikke gennemføres.');}
    finally {session.pendingUploads--;if(!session.destroyed){setUploading(false);if(!session.dirty)notify(session,'synced','Forbundet til dokumentet');}}
  };
  const loaded=()=>{if(media.current){media.current.currentTime=resumeAt.current;if(resumePlaying.current)void media.current.play().catch(()=>{});}};
  const accept=kind==='video'?'video/mp4,video/webm':kind==='image'?'image/png,image/jpeg,image/gif,image/webp':kind==='audio'?'audio/mpeg,audio/wav,audio/ogg':'.mp4,.webm,.mp3,.wav,.ogg,.jpg,.jpeg,.png,.gif,.webp,.pdf,.zip,.docx,.xlsx,.pptx,.txt';
  return <section className="notion-media-block" contentEditable={false} aria-label="Uploadet medie">
    {!assetId && editable && <label className="notion-upload-label">{kind==='video'?'Upload video':kind==='image'?'Upload billede':kind==='audio'?'Upload lyd':'Upload fil'}<input type="file" accept={accept} disabled={uploading} onChange={event=>{const file=event.target.files?.[0];if(file)void upload(file);event.target.value='';}}/><span>Højst 100 MiB pr. fil. Filen deles med holdet, når siden udgives.</span></label>}
    {uploading && <p role="status">Uploader fil… Hold siden åben.</p>}
    {error && <div className="notion-media-error" role="alert">{error}{assetId && <button type="button" onClick={()=>setRetry(value=>value+1)}>Prøv igen</button>}</div>}
    {access && <>
      {access.kind==='video' && <video ref={element=>{media.current=element;}} controls preload="metadata" src={access.url} onLoadedMetadata={loaded} onError={()=>setPlaybackError(true)}>Din browser understøtter ikke denne video. Hent filen nedenfor.</video>}
      {access.kind==='audio' && <audio ref={element=>{media.current=element;}} controls preload="metadata" src={access.url} onLoadedMetadata={loaded} onError={()=>setPlaybackError(true)}/>}
      {access.kind==='image' && <img src={access.url} alt={access.originalName} loading="lazy"/>}
      {access.kind==='pdf' && <a href={access.url} target="_blank" rel="noopener noreferrer">Åbn PDF: {access.originalName}</a>}
      <div className="notion-media-caption"><span>{access.originalName}</span><a href={access.downloadUrl} target="_blank" rel="noopener noreferrer">Hent fil</a></div>
      {playbackError && <p className="notion-block-hint">Mediet kunne ikke afspilles i denne browser. Prøv at hente filen, eller brug MP4/WebM med en browserunderstøttet kodning.</p>}
    </>}
    {assetId && !access && !error && <p role="status">Åbner fil…</p>}
    {!assetId && !editable && <p>Der er endnu ikke tilføjet en fil.</p>}
    {pageId && session && pageId!==session.context.pageId && <p className="notion-media-error">Denne fil tilhører en anden side. Upload filen igen på denne side, før den udgives.</p>}
  </section>;
}
const createMedia=createReactBlockSpec({type:'pageMedia',propSchema:{schemaVersion:{default:1},pageId:{default:''},assetId:{default:''},kind:{default:'download'}},content:'none'},{
  render:({block,editor})=>{const context=useData();return <MediaBlock assetId={block.props.assetId} pageId={block.props.pageId} kind={block.props.kind} editable={Boolean(context?.canEdit && editor.isEditable)} onUpload={(id,kind)=>editor.updateBlock(block,{props:{assetId:id,kind}})}/>;},
  toExternalHTML:()=> <p>Vedhæftet undervisningsfil</p>,
});
const schema = BlockNoteSchema.create({
  blockSpecs: {...defaultBlockSpecs, classRoster:createRoster(),holdPoll:createPoll(),slideBreak:createSlideBreak(),learningGoal:createLearningGoal(),pageLink:createPageLink(),holdGroup:createGroup(),pageMedia:createMedia(),mermaidDiagram:createMermaidDiagram(),callout:createCallout()},
  inlineContentSpecs:{...defaultInlineContentSpecs,pageMention,studentMention},
});
type Editor = BlockNoteEditor<typeof schema.blockSchema, typeof schema.inlineContentSchema, typeof schema.styleSchema>;

interface Session {
  root: Root; context: EditorContext; callbacks?: DotnetCallbacks; doc: Y.Doc; provider?: HocuspocusProvider;
  editor?: Editor; ready: boolean; inSync: boolean; connected: boolean; dirty: boolean; generation: number;
  destroyed: boolean; status: Status; detail: string; presenting: boolean; slides: ReturnType<typeof splitSlides>;
  readOnly:boolean; accessToken:string; pendingUploads:number;
  theme: 'light' | 'dark'; render: () => void; removeListeners: () => void;
}
const sessions = new WeakMap<HTMLElement, Session>();
function notify(session: Session, status: Status, detail: string) {
  if(session.destroyed) return;
  session.status=status; session.detail=detail; session.render();
  void session.callbacks?.invokeMethodAsync('OnNotionStatus',status,detail).catch(() => {});
}
function canEdit(session: Session) { return !session.readOnly && session.context.canEdit && session.connected && session.inSync && !session.presenting; }
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
  const insert=(type:'classRoster'|'holdPoll'|'slideBreak'|'learningGoal'|'pageLink'|'holdGroup'|'pageMedia'|'mermaidDiagram'|'callout',kind='download') => {
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
    else if(type==='holdGroup')add({type,props:{holdId:session.context.holdId}});
    else if(type==='pageMedia')add({type,props:{pageId:session.context.pageId,kind}});
    else if(type==='mermaidDiagram')add({type,props:{source:DEFAULT_MERMAID_SOURCE}});
    else if(type==='callout')add({type,props:{tone:'info'},content:'Skriv din note her…'});
    else add({type});
  };
  const mentionItems=[
    ...(session.context.pages || []).map(page=>({title:page.title,aliases:['side','underside'],group:'Sider',onItemClick:()=>editor.insertInlineContent([{type:'pageMention',props:{pageId:page.id}},' '])})),
    ...session.context.members.map(member=>({title:member.displayName,aliases:['elev'],group:'Elever',onItemClick:()=>editor.insertInlineContent([{type:'studentMention',props:{holdId:session.context.holdId,membershipId:member.id}},' '])})),
  ];
  const customItems=[
    {title:'Link til side',subtext:'Vælg en side eller underside fra holdet',aliases:['side','underside','link'],group:'Undervisning',onItemClick:()=>insert('pageLink')},
    {title:'Gemt gruppe',subtext:'Indsæt en gemt gruppe fra holdet',aliases:['gruppe','grupper'],group:'Undervisning',onItemClick:()=>insert('holdGroup')},
    {title:'Upload video',subtext:'Del MP4 eller WebM med holdet ved udgivelse',aliases:['video','upload'],group:'Medier',onItemClick:()=>insert('pageMedia','video')},
    {title:'Upload billede',subtext:'Tilføj et billede fra din computer',aliases:['billede','foto','image'],group:'Medier',onItemClick:()=>insert('pageMedia','image')},
    {title:'Upload lyd',subtext:'Tilføj MP3, WAV eller OGG',aliases:['lyd','audio'],group:'Medier',onItemClick:()=>insert('pageMedia','audio')},
    {title:'Upload fil',subtext:'PDF, dokument, ZIP eller mediefil',aliases:['fil','vedhæftning','upload'],group:'Medier',onItemClick:()=>insert('pageMedia')},
    {title:'Elever fra holdet',subtext:'Indsæt en levende elevliste',aliases:['elever','klasse','hold','students'],group:'Undervisning',icon:<span aria-hidden="true">◉</span>,onItemClick:()=>insert('classRoster')},
    {title:'Afstemning',subtext:'Kobl en afstemning fra holdet på siden',aliases:['afstemning','poll','stemning'],group:'Undervisning',icon:<span aria-hidden="true">▥</span>,onItemClick:()=>insert('holdPoll')},
    {title:'Slidegrænse',subtext:'Start næste slide i præsentationen',aliases:['slide','slides','præsentation'],group:'Undervisning',icon:<span aria-hidden="true">▱</span>,onItemClick:()=>insert('slideBreak')},
    {title:'Læringsmål',subtext:'Fremhæv målet for aktiviteten',aliases:['læringsmål','mål','learning'],group:'Undervisning',icon:<span aria-hidden="true">◎</span>,onItemClick:()=>insert('learningGoal')},
    {title:'Diagram',subtext:'Mermaid flowchart, sequence m.m.',aliases:['diagram','mermaid','flowchart','graf'],group:'Undervisning',icon:<span aria-hidden="true">◈</span>,onItemClick:()=>insert('mermaidDiagram')},
    {title:'Callout',subtext:'Fremhæv info, tip, advarsel eller vigtigt',aliases:['callout','info','tip','advarsel','note'],group:'Undervisning',icon:<span aria-hidden="true">▣</span>,onItemClick:()=>insert('callout')},
  ];
  return <>
    <div className="notion-editor-tools" role="toolbar" aria-label="Dokumentværktøjer">
      {!session.readOnly && <>
      <button type="button" onClick={()=>editor.undo()} disabled={!enabled} title="Fortryd (Ctrl+Z)">↶ <span>Fortryd</span></button>
      <button type="button" onClick={()=>editor.redo()} disabled={!enabled} title="Gendan (Ctrl+Shift+Z)">↷ <span>Gendan</span></button>
      <span className="notion-tool-divider" />
      <button type="button" onClick={()=>insert('classRoster')} disabled={!enabled}>Elever</button>
      <button type="button" onClick={()=>insert('holdPoll')} disabled={!enabled}>Afstemning</button>
      <button type="button" onClick={()=>insert('slideBreak')} disabled={!enabled}>Slidegrænse</button>
      <button type="button" onClick={()=>insert('pageLink')} disabled={!enabled}>Link til side</button>
      <button type="button" onClick={()=>insert('mermaidDiagram')} disabled={!enabled}>Diagram</button>
      <button type="button" onClick={()=>insert('callout')} disabled={!enabled}>Callout</button>
      <button type="button" onClick={()=>insert('pageMedia','video')} disabled={!enabled}>Upload video</button>
      </>}
      <button type="button" className="notion-present-button" onClick={()=>openPresentation(session)}>Præsenter</button>
    </div>
    {!session.readOnly && !session.inSync && <p className="notion-connection-note" role="status">Redigering fortsætter, når forbindelsen er gendannet. Afventende ændringer bevares i denne fane.</p>}
    <BlockNoteView editor={editor} editable={enabled} theme={session.theme} slashMenu={false} formattingToolbar={false} sideMenu={false}>
      {!session.readOnly && <>
        <FormattingToolbarController formattingToolbar={() => <FormattingToolbar blockTypeSelectItems={turnIntoSelectItems(editor)} />} />
        <SideMenuController sideMenu={(props) => <SideMenu {...props} dragHandleMenu={NotionDragHandleMenu} />} />
        <SuggestionMenuController triggerCharacter="/" getItems={async query=>filterSuggestionItems([...customItems,...getDefaultReactSlashMenuItems(editor)],query)} />
        <SuggestionMenuController triggerCharacter="@" getItems={async query=>filterSuggestionItems(mentionItems,query)} />
      </>}
    </BlockNoteView>
    <footer className="notion-editor-footnote"><span>{session.readOnly ? 'Udgivet undervisningsindhold' : <>Skriv <kbd>/</kbd> for indhold og <kbd>@</kbd> for sider og elever.</>}</span><a href="/notion-editor/THIRD-PARTY-NOTICES.txt" target="_blank" rel="noopener noreferrer">Open source</a></footer>
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
  return <Runtime.Provider value={session}><Context.Provider value={session.context}><div className="notion-blocknote-host" data-notion-theme={session.theme}>
    {session.status==='error' && <p className="notion-editor-persistence-error" role="alert">{session.detail}</p>}
    <EditorBoundary session={session}>{session.ready ? <ConnectedEditor session={session} /> : <div className="notion-editor-loading" role="status">Forbinder til dokumentet…</div>}{session.presenting && <SlideDeck session={session}/>}</EditorBoundary>
  </div></Context.Provider></Runtime.Provider>;
}

export async function mount(element:HTMLElement,context:EditorContext,accessToken:string,dotnetCallbacks?:DotnetCallbacks):Promise<void> {
  destroy(element);
  if(!context.canEdit)return mountReadOnly(element,context,accessToken,dotnetCallbacks);
  if(!context.pageId || !context.holdId || !accessToken) throw new Error('Dokument, hold og login er påkrævet.');
  const session:Session={root:createRoot(element),context,callbacks:dotnetCallbacks,doc:new Y.Doc(),ready:false,inSync:false,connected:false,dirty:false,generation:0,destroyed:false,readOnly:false,accessToken,pendingUploads:0,status:'connecting',detail:'Forbinder til dokumentet…',presenting:false,slides:[],theme:readTheme(),render:()=>{},removeListeners:()=>{}};
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
    url:(location.protocol==='https:'?'wss:':'ws:')+'//'+location.host+'/collaboration?schemaVersion=4',name:'notion:'+String(context.pageId).toLowerCase(),document:session.doc,token:async()=>{if(!session.callbacks)return accessToken;const token=await session.callbacks.invokeMethodAsync('GetNotionToken');if(typeof token!=='string' || !token)throw new Error('Log ind igen for at åbne dokumentet.');return token.startsWith('Bearer ')?token.slice(7):token;},
    onStatus:({status})=>{session.connected=status==='connected';if(!session.connected){session.inSync=false;notify(session,status==='connecting'?'connecting':'offline',status==='connecting'?'Forbinder til dokumentet…':'Forbindelsen er afbrudt. Redigering er sat på pause.');}},
    onSynced:({state})=>{if(!state)return;session.ready=true;session.inSync=true;session.connected=true;notify(session,session.dirty?'saving':'synced',session.dirty?'Gemmer afventende ændringer…':'Forbundet til dokumentet');},
    onAuthenticationFailed:({reason})=>{session.connected=false;session.inSync=false;notify(session,'error',reason.includes('editor_version_mismatch')?'Editoren er opdateret. Genindlæs siden for at åbne dokumentet med den nye version.':'Du har ikke længere adgang, eller dit login er udløbet. Log ind igen.');},
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

/** A published snapshot has no network provider and cannot receive draft updates. */
export async function mountReadOnly(element:HTMLElement,context:EditorContext,accessToken:string,dotnetCallbacks?:DotnetCallbacks):Promise<void> {
  destroy(element);
  if(!context.pageId || !context.holdId || !context.publishedStateBase64)throw new Error('Der er endnu ingen udgivet udgave af siden.');
  if(context.publishedStateBase64.length>24*1024*1024)throw new Error('Den udgivne side er for stor.');
  const doc=new Y.Doc();
  try {Y.applyUpdate(doc,Uint8Array.from(atob(context.publishedStateBase64),value=>value.charCodeAt(0)));}
  catch {doc.destroy();throw new Error('Den udgivne side kunne ikke åbnes.');}
  const session:Session={root:createRoot(element),context:{...context,canEdit:false},callbacks:dotnetCallbacks,doc,ready:true,inSync:true,connected:false,dirty:false,generation:0,destroyed:false,readOnly:true,accessToken,pendingUploads:0,status:'synced',detail:'Udgivet undervisningsindhold',presenting:false,slides:[],theme:readTheme(),render:()=>{},removeListeners:()=>{}};
  sessions.set(element,session);
  session.render=()=>{if(!session.destroyed)session.root.render(<App session={session}/>);};
  const onTheme=()=>{session.theme=readTheme();session.render();};
  const observer=new MutationObserver(onTheme);observer.observe(document.documentElement,{attributes:true,attributeFilter:['data-theme','style','class']});observer.observe(document.body,{attributes:true,attributeFilter:['data-theme','style','class']});
  const media=window.matchMedia('(prefers-color-scheme: dark)');media.addEventListener('change',onTheme);
  session.removeListeners=()=>{observer.disconnect();media.removeEventListener('change',onTheme);};
  notify(session,'synced','Udgivet undervisningsindhold');
}

export async function getPublicationData(element:HTMLElement):Promise<{expectedFingerprint:string;assetIds:string[]}> {
  const session=sessions.get(element);
  if(!session || session.readOnly || !session.editor || !canEdit(session) || session.dirty || session.pendingUploads>0)
    throw new Error('Vent på, at alle ændringer og uploads er gemt i skyen, før du udgiver.');
  const generation=session.generation;
  const fingerprint=canonicalFingerprint(session.doc);
  const assetIds=collectPublicationAssetIds(session.editor.document,session.context.pageId);
  const digest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(fingerprint));
  if(session.destroyed || session.dirty || session.pendingUploads>0 || generation!==session.generation || fingerprint!==canonicalFingerprint(session.doc))
    throw new Error('Dokumentet blev ændret under udgivelsen. Vent på lagring, og prøv igen.');
  return {expectedFingerprint:Array.from(new Uint8Array(digest)).map(value=>value.toString(16).padStart(2,'0')).join(''),assetIds};
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
export function hasUnsavedChanges(element:HTMLElement):boolean {const session=sessions.get(element);return Boolean(session && (session.dirty || session.pendingUploads>0));}
