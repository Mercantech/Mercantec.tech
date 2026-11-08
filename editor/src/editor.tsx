import React, { createContext, useContext, useEffect, useId, useMemo, useRef, useState } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { NativeAiLauncher } from './ai-native-launcher';
import { BlockNoteSchema, defaultBlockSpecs, defaultInlineContentSpecs, trackPosition, type BlockNoteEditor } from '@blocknote/core';
import { filterSuggestionItems, insertOrUpdateBlockForSlashMenu, SuggestionMenu } from '@blocknote/core/extensions';
import { TextSelection } from 'prosemirror-state';
import { withCollaboration } from '@blocknote/core/yjs';
import { createReactBlockSpec, createReactInlineContentSpec, FormattingToolbar, FormattingToolbarController, getDefaultReactEmojiPickerItems, getDefaultReactSlashMenuItems, getFormattingToolbarItems, GridSuggestionMenuController, SideMenu, SideMenuController, SuggestionMenuController, useCreateBlockNote } from '@blocknote/react';
import { BlockNoteView } from '@blocknote/mantine';
import { HocuspocusProvider } from '@hocuspocus/provider';
import * as Y from 'yjs';
import '@blocknote/mantine/style.css';
import './editor.css';
import { danish } from './locale';
import { canonicalFingerprint, publishedDocumentFromBase64, applyReadOnlySnapshot } from './persistence.mjs';
import { pollUrl, pollCreateUrl, selectedMembers, splitSlides, pageUrl, studentUrl, collectPublicationAssetIds } from './document-utils.mjs';
import { createMermaidDiagram, DEFAULT_MERMAID_SOURCE } from './mermaid-block';
import { createCallout } from './callout-block';
import { createNotionCodeBlock, syntaxHighlighter } from './code-block';
import { NotionBlockTypeSelect, NotionDragHandleMenu } from './turn-into';
import { OpenAiOnBlockProvider } from './ai-helper';
import { NativeAiReview, nativeAiReviewExtension } from './ai-native-review';
import { NativeAiPanel } from './ai-native-panel';
import type { AiNativeTarget } from './ai-native-types';
import { formatUploadSize, uploadNotionAsset, uploadNotebookAsset } from './asset-upload.mjs';
import { expandEmojiShortcodes, filterCustomEmojis } from './custom-emoji.mjs';
import { EmojiPicker, type EmojiAnalysis, type EmojiMetadata, type PlatformEmoji } from './emoji-picker';
import { EmojiCatalog, EmojiReload, EmojiCreate, EmojiAnalyze, CustomEmojiImage, EmojiText } from './emoji-context';
import { analyzePlatformEmoji, uploadPlatformEmoji } from './emoji-upload.mjs';
import { EmojiSuggestionMenu } from './emoji-suggestion-menu';
import { ReferenceSuggestionMenu } from './reference-suggestion-menu';
import { captureEmojiQuery, resolveEmojiQuery } from './emoji-query-target.mjs';
import { createDrawing } from './drawing-block';
import { DrawingEnvironmentProvider, requestCreateDrawing, requestOpenDrawing, collectDrawingPublicationData, type DrawingEnvironment } from './drawing-context';
import { useViewport } from './use-viewport';
import { ensureEditorResources } from './editor-resources.mjs';
import { ReferenceProvider, ReferenceView, ReferencePicker, useReferenceSearch, mentionStatusLabel, type EntityReference, type ResolvedReference } from './entity-reference-components';
import { legacyReference, entityReference, referenceKindLabel } from './entity-references.mjs';

export interface EditorContext {
  domain?: 'notion' | 'notebook';
  documentName?: string;
  apiBase?: string;
  revision?: number;
  stateBase64?: string | null;
  canComment?: boolean;
  canShare?: boolean;
  pageId: string;
  holdId?: string | null;
  collectionId?: string | null;
  workspaceId?: string | null;
  courseId?: string | null;
  title: string;
  canEdit: boolean;
  members: { id: string; displayName: string }[];
  polls: { id: string; title: string; state: string }[];
  updatedAtUtc: string;
  userName?: string;
  userSub?: string;
  drawings?: { drawingId:string;blockId:string;mode:string;snapshotId:string|null;revision:number;fingerprint:string }[];
  pages?: {id:string;title:string;parentPageId?:string|null}[];
  groups?: {id:string;name:string;members:{id:string;displayName:string}[]}[];
  publishedStateBase64?:string|null;
  publishedAtUtc?:string|null;
  resourcesLoaded?:boolean;
}
type DotnetCallbacks = { invokeMethodAsync: (method: string, ...args: string[]) => Promise<unknown> };
type Status = 'connecting' | 'synced' | 'saving' | 'saved' | 'offline' | 'error';
const Context = createContext<EditorContext | null>(null);
const useData = () => useContext(Context);
const Runtime = createContext<Session | null>(null);
function useNotionResources(required=true) {
  const session=useContext(Runtime);
  const context=useData();
  useEffect(()=>{
    // The host shows a retryable resource error; document persistence stays usable.
    if(required && session)void ensureEditorResources(session).catch(()=>{});
  },[session,context?.resourcesLoaded,required]);
}
type CustomEmojiRecord = PlatformEmoji;

const createRoster = createReactBlockSpec({
  type: 'classRoster',
  propSchema: { schemaVersion: { default: 1 }, holdId: { default: '' }, membershipIds: { default: '' } },
  content: 'none',
}, {
  render: ({ block, editor }) => {
    const context = useData();
    useNotionResources(context?.holdId===block.props.holdId);
    if (!context || block.props.holdId !== context.holdId) return <div className="notion-block-note">Elevlisten tilhører et andet hold. Indsæt en ny med /elever.</div>;
    if(context.resourcesLoaded===false)return <section className="notion-class-block" contentEditable={false}><p role="status">Henter elever og grupper…</p></section>;
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
      {visible.length ? <table><thead><tr><th scope="col">Elev</th><th scope="col">Grupper</th></tr></thead><tbody>{visible.map((member: {id:string;displayName:string}) => <tr key={member.id}><td><a href={studentUrl(context.holdId,member.id,context) ?? undefined}>{member.displayName}</a></td><td>{context.groups?.filter(group=>group.members.some(item=>item.id===member.id)).map(group=>group.name).join(', ') || '—'}</td></tr>)}</tbody></table> : <p>Ingen elever er valgt.</p>}
      {editable && <GroupButton ids={visible.map((member:{id:string})=>member.id)} />}
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
    useNotionResources(context?.holdId===block.props.holdId);
    const allowed = context && block.props.holdId === context.holdId;
    if(allowed && context.resourcesLoaded===false)return <section className="notion-poll-block" contentEditable={false}><p role="status">Henter afstemninger…</p></section>;
    const poll = allowed ? context.polls.find(item => item.id === block.props.pollId) : undefined;
    const url = poll ? pollUrl(poll.id,false,context) : null;
    const presentUrl = poll ? pollUrl(poll.id,true,context) : null;
    return <section className="notion-poll-block" contentEditable={false} aria-label="Afstemning">
      <header><span className="notion-block-symbol" aria-hidden="true">▥</span><div><strong>{poll?.title || 'Afstemning'}</strong><small>{poll ? ({draft:'Kladde',open:'Åben for svar',closed:'Afsluttet'}[poll.state] || poll.state) : 'Vælg en afstemning fra holdet'}</small></div></header>
      {allowed && context.canEdit && editor.isEditable && <label className="notion-poll-select">Afstemning<select value={poll?.id || ''} onChange={event => editor.updateBlock(block,{props:{pollId:event.target.value}})}><option value="">Vælg afstemning</option>{context.polls.map(item => <option key={item.id} value={item.id}>{item.title}</option>)}</select></label>}
      {url && presentUrl ? <div className="notion-block-actions"><a href={url} target="_blank" rel="noopener noreferrer">Åbn afstemning</a>{context?.canEdit && <a href={presentUrl} target="_blank" rel="noopener noreferrer">Vis på tavlen</a>}</div> : <p className="notion-block-hint">{allowed && !context.polls.length ? <span>Holdet har ingen afstemninger endnu.{context.canEdit && <> <a href={pollCreateUrl(context)} target="_blank" rel="noopener noreferrer">Opret en afstemning</a>.</>}</span> : 'Afstemningen er ikke tilgængelig på dette hold.'}</p>}
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
  render:({inlineContent})=>{const context=useData();return <ReferenceView reference={legacyReference('pageMention',inlineContent.props,context?.domain)}/>;},
});
const studentMention=createReactInlineContentSpec({type:'studentMention',propSchema:{holdId:{default:''},membershipId:{default:''}},content:'none'},{
  render:({inlineContent})=><ReferenceView reference={legacyReference('studentMention',inlineContent.props)}/>,
});
const entityMention=createReactInlineContentSpec({type:'entityMention',propSchema:{kind:{default:''},id:{default:''}},content:'none'},{
  render:({inlineContent})=><ReferenceView reference={entityReference(inlineContent.props.kind,inlineContent.props.id)}/>,
});
const customEmoji=createReactInlineContentSpec({type:'customEmoji',propSchema:{emojiId:{default:''},name:{default:''}},content:'none'},{
  render:({inlineContent})=>{
    const emojis=useContext(EmojiCatalog);
    const match=emojis.find(item=>item.id===inlineContent.props.emojiId);
    const label=`:${match?.name || inlineContent.props.name || 'emoji'}:`;
    return match
      ? <CustomEmojiImage emoji={match} label={label} />
      : <span className="notion-unavailable" contentEditable={false}>{label}</span>;
  },
});
const createPageLink=createReactBlockSpec({type:'pageLink',propSchema:{schemaVersion:{default:1},pageId:{default:''}},content:'none'},{
  render:({block,editor})=>{const context=useData();return <EntityLinkBlock reference={legacyReference('pageLink',block.props,context?.domain)} editable={Boolean(context?.canEdit && editor.isEditable)} onPick={reference=>editor.updateBlock(block,{type:'entityLink',props:reference} as never)}/>;},
  toExternalHTML:()=> <p>Link til undervisningsside</p>,
});
function EntityLinkBlock({reference,editable,onPick}:{reference:EntityReference|null;editable:boolean;onPick:(reference:EntityReference)=>void}) {
  const [selecting,setSelecting]=useState(!reference);
  return <section className="notion-link-block" contentEditable={false} aria-label="Link til indhold i LUP">
    {reference && <ReferenceView reference={reference} block/>}
    {editable && <><button type="button" className="notion-reference-change" onClick={()=>setSelecting(value=>!value)}>{selecting?'Luk søgning':reference?'Skift reference':'Vælg indhold'}</button>{selecting && <ReferencePicker onPick={value=>{onPick(value);setSelecting(false);}}/>}</>}
  </section>;
}
const createEntityLink=createReactBlockSpec({type:'entityLink',propSchema:{schemaVersion:{default:1},kind:{default:''},id:{default:''}},content:'none'},{
  render:({block,editor})=>{const context=useData();return <EntityLinkBlock reference={entityReference(block.props.kind,block.props.id)} editable={Boolean(context?.canEdit && editor.isEditable)} onPick={reference=>editor.updateBlock(block,{props:reference})}/>;},
  toExternalHTML:()=> <p>Reference til indhold i LUP</p>,
});
const createGroup=createReactBlockSpec({type:'holdGroup',propSchema:{schemaVersion:{default:1},holdId:{default:''},groupId:{default:''}},content:'none'},{
  render:({block,editor})=>{const context=useData();const allowed=context?.holdId===block.props.holdId;useNotionResources(allowed);if(allowed && context.resourcesLoaded===false)return <section className="notion-group-block" contentEditable={false}><p role="status">Henter grupper…</p></section>;const group=allowed ? context.groups?.find(item=>item.id===block.props.groupId) : undefined;return <section className="notion-group-block" contentEditable={false} aria-label="Holdgruppe">
    {allowed && context.canEdit && editor.isEditable && <label>Gruppe<select value={group?.id || ''} onChange={event=>editor.updateBlock(block,{props:{groupId:event.target.value}})}><option value="">Vælg gemt gruppe</option>{context.groups?.map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
    {group && context ? <><strong>{group.name}</strong><ul>{group.members.map(member=><li key={member.id}><a href={studentUrl(context.holdId,member.id,context) ?? undefined}>{member.displayName}</a></li>)}</ul></> : <p>Vælg en gruppe fra holdet. Grupper oprettes fra elevlisten eller under holdets grupper.</p>}
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
function UploadConfirmation({file,onConfirm}:{file:File;onConfirm:(confirmed:boolean)=>void}) {
  const dialog=useRef<HTMLDialogElement>(null);
  const titleId=useId();
  const descriptionId=useId();
  useEffect(()=>{
    const previouslyFocused=document.activeElement;
    const element=dialog.current;element?.showModal();
    return()=>{element?.close();if(previouslyFocused instanceof HTMLElement && previouslyFocused.isConnected)previouslyFocused.focus();};
  },[]);
  return <dialog ref={dialog} className="notion-upload-confirm" aria-labelledby={titleId} aria-describedby={descriptionId} onCancel={event=>{event.preventDefault();onConfirm(false);}}>
    <h2 id={titleId}>Upload stor fil?</h2>
    <div id={descriptionId}><p><strong>{file.name}</strong><br/>{formatUploadSize(file.size)}</p><p>Filen er over 100 MiB. Uploaden kan tage tid og bruge en del af din internetforbindelse. Hold siden åben, indtil uploaden er færdig.</p></div>
    <div className="notion-upload-confirm-actions"><button type="button" autoFocus onClick={()=>onConfirm(false)}>Annuller</button><button type="button" onClick={()=>onConfirm(true)}>Upload fil</button></div>
  </dialog>;
}
function MediaBlock({assetId,pageId,kind,onUpload,editable}:{assetId:string;pageId:string;kind:string;onUpload:(id:string,kind:string)=>void;editable:boolean}) {
  const session=useContext(Runtime);
  const [container,nearViewport]=useViewport<HTMLElement>();
  const [access,setAccess]=useState<AssetAccess|null>(null);
  const accessRef=useRef<AssetAccess|null>(null);
  const [playing,setPlaying]=useState(false);
  const [error,setError]=useState('');
  const [uploading,setUploading]=useState(false);
  const [uploadProgress,setUploadProgress]=useState({sent:0,total:0});
  const [uploadName,setUploadName]=useState('');
  const uploadController=useRef<AbortController|null>(null);
  const uploadInput=useRef<HTMLInputElement|null>(null);
  const cancelUploadButton=useRef<HTMLButtonElement|null>(null);
  const [confirmationFile,setConfirmationFile]=useState<File|null>(null);
  const confirmationResolve=useRef<((confirmed:boolean)=>void)|null>(null);
  const [playbackError,setPlaybackError]=useState(false);
  const media=useRef<HTMLMediaElement|null>(null);
  const resumeAt=useRef(0);
  const resumePlaying=useRef(false);
  const [retry,setRetry]=useState(0);
  useEffect(()=>()=>{uploadController.current?.abort();confirmationResolve.current?.(false);},[]);
  useEffect(()=>{
    accessRef.current=null;setAccess(null);setError('');setPlaybackError(false);setPlaying(false);
    resumeAt.current=0;resumePlaying.current=false;
  },[assetId,session]);
  useEffect(()=>{
    if(!assetId || !session || (!nearViewport && !playing))return;
    const controller=new AbortController();let timer:ReturnType<typeof setTimeout>|undefined;
    const schedule=(result:AssetAccess)=>{
      const until=Date.parse(result.expiresAtUtc)-Date.now()-30000;
      timer=setTimeout(()=>void load(),Math.max(15000,Math.min(until,240000)));
    };
    const load=async()=>{
      try {
        const token=await validToken(session);
        controller.signal.throwIfAborted();
        const response=await fetch((session.context.domain==='notebook'?'/api/notebooks':'/api/notion')+'/assets/'+encodeURIComponent(assetId)+'/access',{headers:{Authorization:'Bearer '+token},signal:controller.signal,cache:'no-store'});
        if(!response.ok)throw new Error(response.status===404?'Filen er ikke tilgængelig i denne udgave.':'Filen kunne ikke åbnes. Prøv igen.');
        const result=await response.json() as AssetAccess;
        if(controller.signal.aborted)return;
        if(media.current){resumeAt.current=media.current.currentTime;resumePlaying.current=!media.current.paused;}
        accessRef.current=result;setAccess(result);setError('');
        schedule(result);
      }catch(ex){if(!controller.signal.aborted){setAccess(null);setError(ex instanceof Error?ex.message:'Filen kunne ikke åbnes.');}}
    };
    const current=accessRef.current;
    if(current && Date.parse(current.expiresAtUtc)>Date.now()+30000)schedule(current);
    else void load();
    return()=>{controller.abort();if(timer)clearTimeout(timer);};
  },[assetId,retry,session,nearViewport,playing]);
  const upload=async(file:File)=>{
    if(!session || !editable || uploadController.current)return;
    const controller=new AbortController();uploadController.current=controller;
    setUploading(true);setError('');setUploadName(file.name);setUploadProgress({sent:0,total:file.size});session.pendingUploads++;
    try {
      const result=await (session.context.domain==='notebook'?uploadNotebookAsset:uploadNotionAsset)(file,{
        pageId:session.context.pageId,getToken:()=>validToken(session),signal:controller.signal,
        confirmLargeUpload:(selected:File)=>new Promise<boolean>(resolve=>{confirmationResolve.current=resolve;setConfirmationFile(selected);}),
        onProgress:(sent:number,total:number)=>{
          if(session.destroyed || controller.signal.aborted)return;
          setUploadProgress({sent,total});notify(session,'saving',sent===total?'Færdiggør upload…':'Uploader fil — '+Math.floor(sent/total*100)+' %');
        },
      });
      if(!result || session.destroyed || controller.signal.aborted)return;
      onUpload(result.id,result.kind);
    }catch(ex){if(!session.destroyed && uploadController.current===controller)setError(controller.signal.aborted?'Upload annulleret.':ex instanceof Error?ex.message:'Upload kunne ikke gennemføres.');}
    finally {session.pendingUploads--;if(uploadController.current===controller)uploadController.current=null;if(!session.destroyed){setUploading(false);if(!session.dirty && !session.pendingUploads)notify(session,'synced','Forbundet til dokumentet');}}
  };
  const loaded=()=>{if(media.current){media.current.currentTime=resumeAt.current;if(resumePlaying.current)void media.current.play().catch(()=>{});}};
  const accept=kind==='video'?'video/mp4,video/webm':kind==='image'?'image/png,image/jpeg,image/gif,image/webp':kind==='audio'?'audio/mpeg,audio/wav,audio/ogg':'.mp4,.webm,.mp3,.wav,.ogg,.jpg,.jpeg,.png,.gif,.webp,.pdf,.zip,.docx,.xlsx,.pptx,.txt';
  return <section ref={container} className="notion-media-block" contentEditable={false} aria-label="Uploadet medie">
    {confirmationFile && <UploadConfirmation file={confirmationFile} onConfirm={confirmed=>{setConfirmationFile(null);confirmationResolve.current?.(confirmed);confirmationResolve.current=null;requestAnimationFrame(()=>{if(confirmed)cancelUploadButton.current?.focus();else uploadInput.current?.focus();});}}/>}
    {!assetId && editable && <label className="notion-upload-label">{kind==='video'?'Upload video':kind==='image'?'Upload billede':kind==='audio'?'Upload lyd':'Upload fil'}<input ref={uploadInput} type="file" accept={accept} disabled={uploading} onChange={event=>{const file=event.target.files?.[0];if(file)void upload(file);event.target.value='';}}/><span>{session?.context.domain==='notebook'?'Højst 100 MiB. Filen følger sidens læseadgang.':'Filer over 100 MiB kræver din bekræftelse. Filen deles med holdet, når siden udgives.'}</span></label>}
    {uploading && <div className="notion-upload-progress"><p role="status">{uploadProgress.sent===uploadProgress.total?'Færdiggør upload…':'Uploader '+uploadName+' — '+(uploadProgress.total?Math.floor(uploadProgress.sent/uploadProgress.total*100):0)+' %'} <span>{formatUploadSize(uploadProgress.sent)} af {formatUploadSize(uploadProgress.total)}. Hold siden åben.</span></p><progress max={uploadProgress.total || 1} value={uploadProgress.sent} aria-label="Uploadstatus"/><button ref={cancelUploadButton} type="button" onClick={()=>uploadController.current?.abort()}>Annuller upload</button></div>}
    {error && <div className="notion-media-error" role="alert">{error}{assetId && <button type="button" onClick={()=>{accessRef.current=null;setRetry(value=>value+1);}}>Prøv igen</button>}</div>}
    {access && <>
      {access.kind==='video' && <video ref={element=>{media.current=element;}} controls preload="metadata" src={access.url} onPlay={()=>setPlaying(true)} onPause={()=>setPlaying(false)} onEnded={()=>setPlaying(false)} onLoadedMetadata={loaded} onError={()=>setPlaybackError(true)}>Din browser understøtter ikke denne video. Hent filen nedenfor.</video>}
      {access.kind==='audio' && <audio ref={element=>{media.current=element;}} controls preload="metadata" src={access.url} onPlay={()=>setPlaying(true)} onPause={()=>setPlaying(false)} onEnded={()=>setPlaying(false)} onLoadedMetadata={loaded} onError={()=>setPlaybackError(true)}/>}
      {access.kind==='image' && <img src={access.url} alt={access.originalName} loading="lazy" decoding="async"/>}
      {access.kind==='pdf' && <a href={access.url} target="_blank" rel="noopener noreferrer">Åbn PDF: {access.originalName}</a>}
      <div className="notion-media-caption"><span>{access.originalName}</span><a href={access.downloadUrl} target="_blank" rel="noopener noreferrer">Hent fil</a></div>
      {playbackError && <p className="notion-block-hint">Mediet kunne ikke afspilles i denne browser. Prøv at hente filen, eller brug MP4/WebM med en browserunderstøttet kodning.</p>}
    </>}
    {assetId && !access && !error && <p role="status">{nearViewport?'Åbner fil…':'Mediet indlæses, når det vises.'}</p>}
    {!assetId && !editable && <p>Der er endnu ikke tilføjet en fil.</p>}
    {pageId && session && pageId!==session.context.pageId && <p className="notion-media-error">Denne fil tilhører en anden side. Upload filen igen på denne side, før den udgives.</p>}
  </section>;
}
const createMedia=createReactBlockSpec({type:'pageMedia',propSchema:{schemaVersion:{default:1},pageId:{default:''},assetId:{default:''},kind:{default:'download'}},content:'none'},{
  render:({block,editor})=>{const context=useData();return <MediaBlock assetId={block.props.assetId} pageId={block.props.pageId} kind={block.props.kind} editable={Boolean(context?.canEdit && editor.isEditable)} onUpload={(id,kind)=>editor.updateBlock(block,{props:{assetId:id,kind}})}/>;},
  toExternalHTML:()=> <p>Vedhæftet undervisningsfil</p>,
});
const schema = BlockNoteSchema.create({
  blockSpecs: {...defaultBlockSpecs, codeBlock:createNotionCodeBlock(), classRoster:createRoster(),holdPoll:createPoll(),slideBreak:createSlideBreak(),learningGoal:createLearningGoal(),pageLink:createPageLink(),entityLink:createEntityLink(),holdGroup:createGroup(),pageMedia:createMedia(),mermaidDiagram:createMermaidDiagram(),callout:createCallout(),drawing:createDrawing()},
  inlineContentSpecs:{...defaultInlineContentSpecs,pageMention,studentMention,entityMention,customEmoji},
});
type Editor = BlockNoteEditor<typeof schema.blockSchema, typeof schema.inlineContentSchema, typeof schema.styleSchema>;

interface Session {
  root: Root; element: HTMLElement; context: EditorContext; callbacks?: DotnetCallbacks; doc: Y.Doc; provider?: HocuspocusProvider;
  editor?: Editor; ready: boolean; inSync: boolean; connected: boolean; dirty: boolean; generation: number;
  destroyed: boolean; status: Status; detail: string; presenting: boolean; slides: ReturnType<typeof splitSlides>;
  readOnly:boolean; accessToken:string; pendingUploads:number; drawingDirty?:boolean;
  resourcesTask?:Promise<void>;
  theme: 'light' | 'dark'; render: () => void; removeListeners: () => void;
}
const sessions = new WeakMap<HTMLElement, Session>();
function notify(session: Session, status: Status, detail: string) {
  if(session.destroyed) return;
  if(session.drawingDirty && (status==='saved' || status==='synced')){status='saving';detail='Gemmer tegning…';}
  session.status=status; session.detail=detail; session.render();
  void session.callbacks?.invokeMethodAsync('OnNotionStatus',status,detail).catch(() => {});
}
function canEdit(session: Session) { return !session.readOnly && session.context.canEdit && session.connected && session.inSync && !session.presenting; }
function drawingEnvironment(session:Session):DrawingEnvironment {
  return {domain:session.context.domain==='notebook'?'notebook':'notion',documentId:session.context.pageId,
    published:session.context.domain!=='notebook' && session.readOnly,readOnly:session.readOnly,
    presentation:session.presenting,theme:session.theme,bindings:session.context.drawings,
    getToken:()=>validToken(session),userName:session.context.userName || 'Bruger',userSub:session.context.userSub,
    onDirty:dirty=>{if(session.destroyed || Boolean(session.drawingDirty)===dirty)return;session.drawingDirty=dirty;
      if(dirty)notify(session,'saving','Gemmer tegning…');else if(!session.dirty)notify(session,'saved','Gemt i skyen');}};
}
function readTheme(): 'light' | 'dark' {
  const root = document.documentElement;
  const resolved = root.dataset.bsTheme;
  if (resolved === 'light' || resolved === 'dark') return resolved;
  const scheme = getComputedStyle(root).colorScheme.trim().replace(/^only\s+/, '');
  if (scheme === 'light' || scheme === 'dark') return scheme;
  return matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}
function observeTheme(session: Session): () => void {
  const sync = () => {
    const theme = readTheme();
    if (theme === session.theme) return;
    session.theme = theme;
    session.render();
  };
  const observer = new MutationObserver(sync);
  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['data-bs-theme', 'data-theme', 'style', 'class'],
  });
  const media = matchMedia('(prefers-color-scheme: dark)');
  media.addEventListener('change', sync);
  return () => {
    observer.disconnect();
    media.removeEventListener('change', sync);
  };
}
function openPresentation(session: Session) {
  if(!session.editor) return;
  session.slides=splitSlides(session.editor.document);
  session.presenting=true; session.render();
}

function ConnectedEditor({session}:{session:Session}) {
  const aiReview=useMemo(()=>new NativeAiReview(session.doc),[session]);
  const editor=useCreateBlockNote(withCollaboration({
    schema, dictionary:danish, extensions:[syntaxHighlighter,nativeAiReviewExtension(aiReview)],
    collaboration:{fragment:session.doc.getXmlFragment('document-store'),provider:{awareness:session.provider?.awareness ?? undefined},user:{name:session.context.userName || 'Underviser',color:'#2452e8'},showCursorLabels:'activity'},
  }),[session]);
  session.editor=editor;
  useEffect(() => () => { session.editor=undefined; },[session]);
  const enabled=canEdit(session);
  const notebook=session.context.domain==='notebook';
  const referenceSearch=useReferenceSearch();
  const emojis=useContext(EmojiCatalog);
  const [emojiOpen,setEmojiOpen]=useState(false);
  const [emojiUploadFile,setEmojiUploadFile]=useState<File|null>(null);
  const [emojiInsertError,setEmojiInsertError]=useState('');
  const colonFileInput=useRef<HTMLInputElement>(null);
  const queuedEmojiTarget=useRef<ReturnType<typeof captureEmojiQuery>|null>(null);
  const activeEmojiTarget=useRef<ReturnType<typeof captureEmojiQuery>|null>(null);
  const createEmoji=useContext(EmojiCreate);
  const analyzeEmoji=useContext(EmojiAnalyze);
  const reloadEmojis=useContext(EmojiReload);
  const emojiPanelId=useId();
  const closeEmoji=()=>{
    const target=activeEmojiTarget.current ?? queuedEmojiTarget.current;
    setEmojiOpen(false);
    setEmojiUploadFile(null);
    queuedEmojiTarget.current=null;
    activeEmojiTarget.current=null;
    editor.focus();
    if(!target)return;
    try {
      const range=resolveEmojiQuery(editor,target);
      editor.transact(tr=>{tr.setSelection(TextSelection.create(tr.doc,range.to));});
    } catch { /* A changed target keeps the editor's current selection and text. */ }
  };
  const openEmoji=()=>{
    if(!canEdit(session)||notebook)return;
    queuedEmojiTarget.current=null;
    activeEmojiTarget.current=null;
    setEmojiUploadFile(null);
    setEmojiInsertError('');
    reloadEmojis();
    setEmojiOpen(true);
  };
  const startColonUpload=()=>{
    if(!canEdit(session)||!createEmoji)return;
    const menu=editor.getExtension(SuggestionMenu);
    const state=menu?.store.state;
    if(!state?.show||state.triggerCharacter!==':')return;
    try {
      queuedEmojiTarget.current=captureEmojiQuery(editor,state.query,trackPosition);
      setEmojiInsertError('');
      menu!.closeMenu();
      colonFileInput.current?.click();
    } catch(error) { setEmojiInsertError(error instanceof Error?error.message:'Vælg indsættelsesstedet igen.'); }
  };
  const chooseColonFile=(event:React.ChangeEvent<HTMLInputElement>)=>{
    const file=event.target.files?.[0];
    event.target.value='';
    if(!file||!canEdit(session)||!queuedEmojiTarget.current){queuedEmojiTarget.current=null;return;}
    activeEmojiTarget.current=queuedEmojiTarget.current;
    queuedEmojiTarget.current=null;
    setEmojiUploadFile(file);
    setEmojiOpen(true);
  };
  const [aiOpen,setAiOpen]=useState(false);
  const [aiAnchor,setAiAnchor]=useState<AiNativeTarget|null>(null);
  const aiLayoutRoot=useMemo(()=>session.element.closest<HTMLElement>('.notion-workspace'),[session]);
  const aiToolbarSlot=useMemo(()=>aiLayoutRoot?.querySelector<HTMLElement>('.notion-ai-toggle-slot')??null,[aiLayoutRoot]);
  const [layoutFocused,setLayoutFocused]=useState(()=>aiLayoutRoot?.dataset.focusMode==='true');
  useEffect(()=>{
    if(!aiLayoutRoot)return;
    const focusChanged=(event:Event)=>{
      const focused=(event as CustomEvent<{focused?:unknown}>).detail?.focused;
      setLayoutFocused(typeof focused==='boolean'?focused:aiLayoutRoot.dataset.focusMode==='true');
    };
    setLayoutFocused(aiLayoutRoot.dataset.focusMode==='true');
    aiLayoutRoot.addEventListener('notion-layout-focus',focusChanged);
    return()=>aiLayoutRoot.removeEventListener('notion-layout-focus',focusChanged);
  },[aiLayoutRoot]);
  const aiVisible=aiOpen&&!layoutFocused;
  const aiPanelId=useId();
  const aiLauncher=useRef<HTMLButtonElement>(null);
  const aiReturnFocus=useRef<HTMLElement|null>(null);
  const revealAi=()=>{
    if(!aiVisible && document.activeElement instanceof HTMLElement && document.activeElement!==document.body)aiReturnFocus.current=document.activeElement;
    aiLayoutRoot?.dispatchEvent(new CustomEvent('notion-layout-reveal',{detail:{panel:'ai'}}));
    setLayoutFocused(false);
    setAiOpen(true);
  };
  const closeAi=()=>{
    setAiOpen(false);
    requestAnimationFrame(()=>{
      if(session.destroyed)return;
      const previous=aiReturnFocus.current;
      if(previous?.isConnected && previous.getClientRects().length && !previous.closest('.native-ai-panel'))previous.focus({preventScroll:true});
      else if(aiLauncher.current)aiLauncher.current.focus({preventScroll:true});
      else editor.focus();
    });
  };
  const openAi=(blockId?:string)=>{
    if(!canEdit(session) || notebook)return;
    try { setAiAnchor(aiReview.capture(blockId)); revealAi(); }
    catch(error) { setEmojiInsertError(error instanceof Error?error.message:'Vælg et sted i dokumentet først.'); }
  };
  const placeEmoji=(item:CustomEmojiRecord)=>{
    if(!canEdit(session))return;
    const node={type:'customEmoji' as const,props:{emojiId:item.id,name:item.name}};
    if(activeEmojiTarget.current){
      try {
        const range=resolveEmojiQuery(editor,activeEmojiTarget.current);
        editor.focus();
        editor.transact(tr=>{
          tr.delete(range.from,range.to);
          tr.setSelection(TextSelection.create(tr.doc,range.from));
          editor.insertInlineContent([node,' ']);
        });
        activeEmojiTarget.current=null;
        setEmojiUploadFile(null);
        setEmojiInsertError('');
        setEmojiOpen(false);
      } catch(error) {
        setEmojiInsertError('Emojien er gemt i biblioteket, men teksten ved indsættelsesstedet er ændret. Vælg stedet igen.');
        closeEmoji();
      }
      return;
    }
    editor.focus();
    setEmojiInsertError('');
    try { editor.insertInlineContent([node,' ']); }
    catch {
      try {
        const cursor=editor.getTextCursorPosition().block;
        const inserted=editor.insertBlocks([{type:'paragraph',content:[node]}],cursor,'after');
        editor.setTextCursorPosition(inserted[0],'end');
      } catch { /* Void blocks without a following paragraph keep their content. */ }
    }
    setEmojiOpen(false);
  };
  useEffect(()=>{
    if(session.readOnly || notebook)return;
    let expanding=false;
    return editor.onChange((_,ctx)=>{
      if(expanding || !canEdit(session) || emojis.length===0)return;
      const replacements:{id:string;content:unknown}[]=[];
      for(const change of ctx.getChanges()){
        if(change.type==='delete' || change.block.type==='codeBlock' || !Array.isArray(change.block.content))continue;
        const expanded=expandEmojiShortcodes(change.block.content,emojis);
        if(expanded.changed) replacements.push({id:change.block.id,content:expanded.content});
      }
      if(!replacements.length)return;
      expanding=true;
      queueMicrotask(()=>{
        try {
          if(session.destroyed)return;
          for(const item of replacements){
            const block=editor.getBlock(item.id);
            if(!block)continue;
            editor.updateBlock(block,{content:item.content as never});
          }
        } finally { expanding=false; }
      });
    },false);
  },[editor,emojis,session]);;
  const insert=(type:'classRoster'|'holdPoll'|'slideBreak'|'learningGoal'|'entityLink'|'holdGroup'|'pageMedia'|'mermaidDiagram'|'callout',kind='download') => {
    if(!enabled) return;
    editor.focus();
    const cursor=editor.getTextCursorPosition().block;
    const add=(block: Parameters<Editor['insertBlocks']>[0][number])=>{
      if(cursor.content!==undefined) insertOrUpdateBlockForSlashMenu(editor,block);
      else {const inserted=editor.insertBlocks([block,{type:'paragraph'}],cursor,'after');editor.setTextCursorPosition(inserted[1],'end');}
    };
    if(type==='classRoster') add({type,props:{holdId:session.context.holdId || ''}});
    else if(type==='holdPoll') add({type,props:{holdId:session.context.holdId || ''}});
    else if(type==='learningGoal') add({type,content:'Efter aktiviteten kan du…'});
    else if(type==='holdGroup')add({type,props:{holdId:session.context.holdId || ''}});
    else if(type==='pageMedia')add({type,props:{pageId:session.context.pageId,kind}});
    else if(type==='mermaidDiagram')add({type,props:{source:DEFAULT_MERMAID_SOURCE}});
    else if(type==='callout')add({type,props:{tone:'info'},content:'Skriv din note her…'});
    else add({type});
  };
  const insertDrawing=async()=>{
    if(!enabled)return;
    editor.focus();
    const cursor=editor.getTextCursorPosition().block;
    const empty=cursor.type==='paragraph' && Array.isArray(cursor.content) && cursor.content.length===0;
    const block=empty ? (editor.updateBlock(cursor.id,{type:'drawing',props:{drawingId:''}}),editor.getBlock(cursor.id)!)
      : editor.insertBlocks([{type:'drawing',props:{drawingId:''}}],cursor,'after')[0];
    const next=editor.insertBlocks([{type:'paragraph'}],block,'after')[0];
    editor.setTextCursorPosition(next,'start');
    try {
      const created=await requestCreateDrawing(drawingEnvironment(session),block.id);
      if(session.destroyed || !editor.getBlock(block.id))return;
      editor.updateBlock(block.id,{props:{drawingId:created.id}});
      requestOpenDrawing(block.id);
    } catch(error) {setEmojiInsertError(error instanceof Error?error.message:'Tegningen kunne ikke oprettes. Prøv igen.');}
  };
  const customItems=[
    {title:'Tegning',aliases:['tegning','excalidraw','canvas','whiteboard'],group:'Medier',onItemClick:()=>{void insertDrawing();}},
    {title:'Link til indhold',subtext:'Find sider, personer, opgaver og andet indhold i LUP',aliases:['side','underside','link','reference','notesbog'],group:'Undervisning',onItemClick:()=>insert('entityLink')},
    {title:'Gemt gruppe',subtext:'Indsæt en gemt gruppe fra holdet',aliases:['gruppe','grupper'],group:'Undervisning',onItemClick:()=>insert('holdGroup')},
    {title:'Upload video',subtext:notebook?'Tilføj MP4 eller WebM til siden':'Del MP4 eller WebM med holdet ved udgivelse',aliases:['video','upload'],group:'Medier',onItemClick:()=>insert('pageMedia','video')},
    {title:'Upload billede',subtext:'Tilføj et billede fra din computer',aliases:['billede','foto','image'],group:'Medier',onItemClick:()=>insert('pageMedia','image')},
    {title:'Upload lyd',subtext:'Tilføj MP3, WAV eller OGG',aliases:['lyd','audio'],group:'Medier',onItemClick:()=>insert('pageMedia','audio')},
    {title:'Upload fil',subtext:'PDF, dokument, ZIP eller mediefil',aliases:['fil','vedhæftning','upload'],group:'Medier',onItemClick:()=>insert('pageMedia')},
    {title:'Emoji',subtext:'Vælg en emoji eller tilføj din egen',aliases:['emoji','emojis','smiley'],group:'Medier',onItemClick:openEmoji},
    {title:'Elever fra holdet',subtext:'Indsæt en levende elevliste',aliases:['elever','klasse','hold','students'],group:'Undervisning',icon:<span aria-hidden="true">◉</span>,onItemClick:()=>insert('classRoster')},
    {title:'Afstemning',subtext:'Kobl en afstemning fra holdet på siden',aliases:['afstemning','poll','stemning'],group:'Undervisning',icon:<span aria-hidden="true">▥</span>,onItemClick:()=>insert('holdPoll')},
    {title:'Slidegrænse',subtext:'Start næste slide i præsentationen',aliases:['slide','slides','præsentation'],group:'Undervisning',icon:<span aria-hidden="true">▱</span>,onItemClick:()=>insert('slideBreak')},
    {title:'Læringsmål',subtext:'Fremhæv målet for aktiviteten',aliases:['læringsmål','mål','learning'],group:'Undervisning',icon:<span aria-hidden="true">◎</span>,onItemClick:()=>insert('learningGoal')},
    {title:'Diagram',subtext:'Mermaid flowchart, sequence m.m.',aliases:['diagram','mermaid','flowchart','graf'],group:'Undervisning',icon:<span aria-hidden="true">◈</span>,onItemClick:()=>insert('mermaidDiagram')},
    {title:'Callout',subtext:'Fremhæv info, tip, advarsel eller vigtigt',aliases:['callout','info','tip','advarsel','note'],group:'Undervisning',icon:<span aria-hidden="true">▣</span>,onItemClick:()=>insert('callout')},
    {title:'AI',subtext:'Generér blokke, omskriv tekst eller lav et diagram',aliases:['ai','openai','hjælp','assistent'],group:'Undervisning',icon:<span aria-hidden="true">✦</span>,onItemClick:()=>openAi()},
    {title:'Præsenter',subtext:'Vis dokumentet som slides',aliases:['præsenter','præsentation','slides','presentation'],group:'Visning',onItemClick:()=>openPresentation(session)},
  ].filter(item=>(!notebook || !['Gemt gruppe','Elever fra holdet','Afstemning','Slidegrænse','Emoji','AI','Præsenter'].includes(item.title)) && (session.context.holdId || !['Gemt gruppe','Elever fra holdet','Afstemning'].includes(item.title)));
  return <OpenAiOnBlockProvider openOnBlock={notebook?null:openAi}>
    {!session.readOnly && !notebook && enabled && createEmoji && <input ref={colonFileInput} className="notion-emoji-file notion-emoji-colon-file" type="file" accept="image/png,image/jpeg,image/gif,image/webp" aria-label="Vælg emoji-billede fra kolonmenu" onChange={chooseColonFile} tabIndex={-1} />}
    {emojiInsertError && <p className="notion-editor-persistence-error" role="alert">{emojiInsertError}</p>}
    {!session.readOnly && !notebook && emojiOpen && <EmojiPicker id={emojiPanelId} emojis={emojis} onPick={placeEmoji} onClose={closeEmoji} onUpload={enabled ? createEmoji : undefined} onAnalyze={enabled ? analyzeEmoji : undefined} initialFile={emojiUploadFile} />}
    {!session.readOnly && !session.inSync && <p className="notion-connection-note" role="status">Redigering fortsætter, når forbindelsen er gendannet. Afventende ændringer bevares i denne fane.</p>}
    <div className="native-ai-workspace" data-ai-open={!session.readOnly && !notebook && aiVisible}>
    <div className="native-ai-document">
    {!session.readOnly && !notebook && <NativeAiLauncher slot={aiToolbarSlot} buttonRef={aiLauncher} panelId={aiPanelId} visible={aiVisible} enabled={enabled} onToggle={()=>{if(aiVisible)closeAi();else openAi();}} />}
    <BlockNoteView editor={editor} editable={enabled} theme={session.theme} slashMenu={false} formattingToolbar={false} sideMenu={false} emojiPicker={false}>
      {!session.readOnly && <>
        <FormattingToolbarController formattingToolbar={() => <FormattingToolbar><NotionBlockTypeSelect />{getFormattingToolbarItems().filter((item) => item.key !== 'blockTypeSelect')}{!notebook && <button type="button" className="native-ai-selection-button" onMouseDown={event=>event.preventDefault()} onClick={()=>openAi()} disabled={!enabled}>✦ AI</button>}</FormattingToolbar>} />
        <SideMenuController sideMenu={(props) => <SideMenu {...props} dragHandleMenu={NotionDragHandleMenu} />} />
        <SuggestionMenuController triggerCharacter="/" getItems={async query=>filterSuggestionItems([...customItems,...getDefaultReactSlashMenuItems(editor)],query)} />
        <SuggestionMenuController triggerCharacter="@" suggestionMenuComponent={ReferenceSuggestionMenu} getItems={async (query):Promise<{title:string;subtext:string;group:string;onItemClick:()=>void}[]>=>{
          try {
            const result=await referenceSearch?.search(query);
            return session.destroyed?[]:(result?.items || []).map((item:ResolvedReference)=>({title:item.title,subtext:[referenceKindLabel(item.reference.kind),item.contextLabel,mentionStatusLabel(item.mentionStatus)].filter(Boolean).join(' · '),group:'Indhold i LUP',onItemClick:()=>{if(canEdit(session))editor.insertInlineContent([{type:'entityMention',props:item.reference},' ']);}}));
          } catch(error) { if(!session.destroyed)setEmojiInsertError(error instanceof Error?error.message:'Søgningen kunne ikke hentes.');return []; }
        }} />
        {!notebook && <GridSuggestionMenuController triggerCharacter=":" columns={6} minQueryLength={2} floatingUIOptions={{elementProps:{style:{zIndex:100}}}} gridSuggestionMenuComponent={props=><EmojiSuggestionMenu {...props} onAdd={enabled && createEmoji ? startColonUpload : undefined} />} getItems={async query=>{
          const custom=filterCustomEmojis(emojis,query).map((item:CustomEmojiRecord)=>({
            id:`custom-${item.id}`,
            label:`:${item.name}:`,description:item.altText,
            icon:<img className="notion-custom-emoji" src={item.contentUrl} alt="" />,
            onItemClick:()=>placeEmoji(item),
          }));
          try { return [...custom, ...await getDefaultReactEmojiPickerItems(editor,query)]; }
          catch { return custom; }
        }} />}
      </>}
    </BlockNoteView>
    </div>
    {!session.readOnly && !notebook && <NativeAiPanel review={aiReview} pageId={session.context.pageId} pageTitle={session.context.title} panelId={aiPanelId} layoutRoot={aiLayoutRoot} enabled={enabled} open={aiVisible} target={aiAnchor} onClose={closeAi} onReveal={revealAi} onCapture={()=>openAi()} getToken={()=>validToken(session)} />}
    </div>
  </OpenAiOnBlockProvider>;
}
function SlideContent({blocks,theme}:{blocks:ReturnType<typeof splitSlides>[number];theme:'light'|'dark'}) {
  const editor=useCreateBlockNote({schema,dictionary:danish,extensions:[syntaxHighlighter],initialContent:blocks as NonNullable<Parameters<typeof useCreateBlockNote>[0]>['initialContent']});
  return <BlockNoteView editor={editor} editable={false} theme={theme} formattingToolbar={false} sideMenu={false} slashMenu={false} emojiPicker={false} />;
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
    <header className="notion-slide-header"><strong><EmojiText text={session.context.title} /></strong><span>{index+1} / {total}</span><button type="button" autoFocus onClick={close}>Luk præsentation</button></header>
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
  const [emojis,setEmojis]=useState<CustomEmojiRecord[]>([]);
  const emojiRevision=useRef(0);
  const loadRef=useRef<() => Promise<void>>(async()=>{});
  loadRef.current=async()=>{
    try {
      const revision=emojiRevision.current;
      const token=await validToken(session);
      const response=await fetch('/api/emojis',{headers:{Authorization:'Bearer '+token},cache:'no-store'});
      if(!response.ok || session.destroyed)return;
      const data=await response.json();
      if(!Array.isArray(data) || session.destroyed || revision!==emojiRevision.current)return;
      const next:CustomEmojiRecord[]=[];
      for(const item of data){
        if(item && typeof item.id==='string' && typeof item.name==='string' && typeof item.contentUrl==='string')
          next.push({id:item.id,name:item.name,contentUrl:item.contentUrl,isStandard:item.isStandard===true,category:typeof item.category==='string' ? item.category : 'custom',keywords:Array.isArray(item.keywords) ? item.keywords.filter((word:unknown):word is string=>typeof word==='string') : [],altText:typeof item.altText==='string' ? item.altText : ''});
      }
      setEmojis(next);
    } catch { /* Editoren virker også uden emoji-biblioteket. */ }
  };
  const createEmoji=async(file:File,name:string,signal:AbortSignal,metadata:EmojiMetadata):Promise<CustomEmojiRecord>=>{
    if(!canEdit(session))throw new Error('Du har ikke adgang til at tilføje emojis her.');
    const created=await uploadPlatformEmoji(file,name,{getToken:()=>validToken(session),signal,metadata});
    if(session.destroyed || signal.aborted)throw new DOMException('Uploaden er afsluttet.', 'AbortError');
    emojiRevision.current++;
    setEmojis(current=>[...current.filter(item=>item.id!==created.id),created]);
    return created;
  };
  const analyzeEmoji=async(file:File,signal:AbortSignal):Promise<EmojiAnalysis>=>{
    if(!canEdit(session))throw new Error('Du har ikke adgang til at analysere emojis her.');
    const result=await analyzePlatformEmoji(file,{getToken:()=>validToken(session),signal});
    if(session.destroyed || signal.aborted)throw new DOMException('Analysen er afsluttet.', 'AbortError');
    return result;
  };
  useEffect(()=>{void loadRef.current();},[session]);
  const drawingEnv=useMemo(()=>drawingEnvironment(session),[session,session.context,session.theme,session.presenting]);
  const referenceEnv={sourceKind:session.context.domain==='notebook'?'notebook-page':'notion-page',sourceId:session.context.pageId,holdId:session.context.holdId,publishedOnly:session.context.domain!=='notebook' && session.readOnly,apiBase:session.context.apiBase || '/api',getToken:()=>validToken(session),revision:session.context.revision ?? session.context.updatedAtUtc,canEdit:canEdit(session)};
  return <Runtime.Provider value={session}><Context.Provider value={session.context}><EmojiCatalog.Provider value={emojis}><EmojiReload.Provider value={()=>{void loadRef.current();}}><EmojiCreate.Provider value={session.context.domain==='notebook' ? undefined : createEmoji}><EmojiAnalyze.Provider value={session.context.domain==='notebook' ? undefined : analyzeEmoji}><div className="notion-blocknote-host" data-notion-theme={session.theme}>
    {session.status==='error' && <p className="notion-editor-persistence-error" role="alert">{session.detail}</p>}
    <ReferenceProvider environment={referenceEnv}><DrawingEnvironmentProvider value={drawingEnv}><EditorBoundary session={session}>{session.ready ? <ConnectedEditor session={session} /> : <div className="notion-editor-loading" role="status">Forbinder til dokumentet…</div>}{session.presenting && <SlideDeck session={session}/>}</EditorBoundary></DrawingEnvironmentProvider></ReferenceProvider>
  </div></EmojiAnalyze.Provider></EmojiCreate.Provider></EmojiReload.Provider></EmojiCatalog.Provider></Context.Provider></Runtime.Provider>;
}

export async function mount(element:HTMLElement,context:EditorContext,accessToken:string,dotnetCallbacks?:DotnetCallbacks):Promise<void> {
  destroy(element);
  if(!context.canEdit)return mountReadOnly(element,context,accessToken,dotnetCallbacks);
  if(!context.pageId || !accessToken) throw new Error('Dokument og login er påkrævet.');
  const session:Session={root:createRoot(element),element,context,callbacks:dotnetCallbacks,doc:new Y.Doc(),ready:false,inSync:false,connected:false,dirty:false,generation:0,destroyed:false,readOnly:false,accessToken,pendingUploads:0,status:'connecting',detail:'Forbinder til dokumentet…',presenting:false,slides:[],theme:readTheme(),render:()=>{},removeListeners:()=>{}};
  sessions.set(element,session);
  session.render=()=>{if(!session.destroyed)session.root.render(<App session={session}/>);};
  const onBeforeUnload=(event:BeforeUnloadEvent)=>{if(session.dirty || session.pendingUploads>0 || session.drawingDirty){event.preventDefault();event.returnValue='';}};
  window.addEventListener('beforeunload',onBeforeUnload);
  const removeThemeListener=observeTheme(session);
  session.removeListeners=()=>{window.removeEventListener('beforeunload',onBeforeUnload);removeThemeListener();};
  session.doc.on('update',(_update:Uint8Array,origin:unknown)=>{
    if(origin!==session.provider && session.ready && !session.destroyed){session.dirty=true;session.generation++;notify(session,session.connected?'saving':'offline',session.connected?'Gemmer i skyen…':'Forbindelsen er afbrudt. Ændringer afventer lagring.');}
  });
  session.provider=new HocuspocusProvider({
    url:(location.protocol==='https:'?'wss:':'ws:')+'//'+location.host+'/collaboration?schemaVersion=8',name:(context.domain==='notebook'?'notebook:':'notion:')+String(context.pageId).toLowerCase(),document:session.doc,token:async()=>{if(!session.callbacks)return accessToken;const token=await session.callbacks.invokeMethodAsync('GetNotionToken');if(typeof token!=='string' || !token)throw new Error('Log ind igen for at åbne dokumentet.');return token.startsWith('Bearer ')?token.slice(7):token;},
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
  const snapshot=context.domain==='notebook'?(context.stateBase64 ?? context.publishedStateBase64):context.publishedStateBase64;
  if(!context.pageId || !snapshot)throw new Error('Der er endnu ingen udgivet udgave af siden.');
  if(snapshot.length>24*1024*1024)throw new Error('Den udgivne side er for stor.');
  const doc=new Y.Doc();
  try {Y.applyUpdate(doc,Uint8Array.from(atob(snapshot),value=>value.charCodeAt(0)));}
  catch {doc.destroy();throw new Error('Den udgivne side kunne ikke åbnes.');}
  const session:Session={root:createRoot(element),element,context:{...context,canEdit:false},callbacks:dotnetCallbacks,doc,ready:true,inSync:true,connected:false,dirty:false,generation:0,destroyed:false,readOnly:true,accessToken,pendingUploads:0,status:'synced',detail:'Udgivet undervisningsindhold',presenting:false,slides:[],theme:readTheme(),render:()=>{},removeListeners:()=>{}};
  sessions.set(element,session);
  session.render=()=>{if(!session.destroyed)session.root.render(<App session={session}/>);};
  const removeThemeListener=observeTheme(session);
  const onBeforeUnload=(event:BeforeUnloadEvent)=>{if(session.drawingDirty){event.preventDefault();event.returnValue='';}};
  window.addEventListener('beforeunload',onBeforeUnload);
  session.removeListeners=()=>{removeThemeListener();window.removeEventListener('beforeunload',onBeforeUnload);};
  notify(session,'synced',context.domain==='notebook'?'Senest gemte indhold':'Udgivet undervisningsindhold');
}

export async function getPublicationData(element:HTMLElement):Promise<{expectedFingerprint:string;assetIds:string[];drawings:Awaited<ReturnType<typeof collectDrawingPublicationData>>}> {
  const session=sessions.get(element);
  if(!session || session.readOnly || !session.editor || !canEdit(session) || session.dirty || session.pendingUploads>0 || session.drawingDirty)
    throw new Error('Vent på, at alle ændringer og uploads er gemt i skyen, før du udgiver.');
  const generation=session.generation;
  const fingerprint=canonicalFingerprint(session.doc);
  const assetIds=collectPublicationAssetIds(session.editor.document,session.context.pageId);
  const drawings=await collectDrawingPublicationData(drawingEnvironment(session),session.editor.document);
  const digest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(fingerprint));
  if(session.destroyed || session.dirty || session.pendingUploads>0 || session.drawingDirty || generation!==session.generation || fingerprint!==canonicalFingerprint(session.doc))
    throw new Error('Dokumentet blev ændret under udgivelsen. Vent på lagring, og prøv igen.');
  return {expectedFingerprint:Array.from(new Uint8Array(digest)).map(value=>value.toString(16).padStart(2,'0')).join(''),assetIds,drawings};
}
export function destroy(element:HTMLElement):void {
  const session=sessions.get(element);if(!session)return;
  session.destroyed=true;session.removeListeners();session.root.unmount();session.provider?.destroy();session.doc.destroy();sessions.delete(element);
}
export function setContext(element:HTMLElement,context:EditorContext):void {
  const session=sessions.get(element);if(!session)return;
  if(context.pageId!==session.context.pageId || context.domain!==session.context.domain || (context.domain!=='notebook' && context.holdId!==session.context.holdId))throw new Error('Dokumentskift kræver en ny editor.');
  session.context=context;session.render();
}
/** Notebook readers refresh saved state without remounting the React/BlockNote editor. */
export function applyReadOnlyState(element:HTMLElement,context:EditorContext):void {
  const session=sessions.get(element);
  if(!session || !session.readOnly || session.context.domain!=='notebook' || context.domain!=='notebook')
    throw new Error('Kun notesbogens læsevisning kan opdateres.');
  if(context.pageId!==session.context.pageId)throw new Error('Dokumentskift kræver en ny editor.');
  if(context.revision!==undefined && session.context.revision!==undefined && context.revision<session.context.revision)return;
  const snapshot=context.stateBase64 ?? context.publishedStateBase64;
  const scrollX=window.scrollX,scrollY=window.scrollY;
  if(snapshot)applyReadOnlySnapshot(session.doc,snapshot);
  setContext(element,{...context,canEdit:false});
  // Remote Yjs updates preserve relative selections; retain the reader's viewport too.
  window.scrollTo(scrollX,scrollY);
}
export function setPresentation(element:HTMLElement,presenting:boolean):void {
  const session=sessions.get(element);if(!session)return;
  if(presenting)openPresentation(session);else{session.presenting=false;session.render();}
}
export function hasUnsavedChanges(element:HTMLElement):boolean {const session=sessions.get(element);return Boolean(session && (session.dirty || session.pendingUploads>0 || session.drawingDirty));}
