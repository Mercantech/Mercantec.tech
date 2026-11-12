/** Fictional, deterministic browser data. This module never reads environment secrets or calls a network. */
export const FIXED_TIME = '2026-11-09T09:00:00.000Z';
export const fixtureId = number => `${String(number).padStart(8, '0')}-0000-4000-8000-000000000001`;
export const ids = Object.freeze({ workspace: fixtureId(11), hold: fixtureId(22), course: fixtureId(33),
  quest: fixtureId(44), submission: fixtureId(55), revision: fixtureId(56), file: fixtureId(57),
  groupSet: fixtureId(66), page: fixtureId(77), group: fixtureId(78), student: fixtureId(88), teacher: fixtureId(89), notebook: fixtureId(90) });
const { workspace: W, hold: H, course: C, quest: Q, submission: S, revision: R, file: F,
  groupSet: SET, page: P, group: G, student: U, teacher: T, notebook: N } = ids;
const stamp = FIXED_TIME;
// A Yjs document containing one fictional teaching paragraph; generated once from a fixed client id.
const documentSnapshot = 'AQmy8hkABwEOZG9jdW1lbnQtc3RvcmUDCmJsb2NrR3JvdXAHALLyGQADDmJsb2NrQ29udGFpbmVyBwCy8hkBAwlwYXJhZ3JhcGgHALLyGQIGBACy8hkDjAFGb3JiaW5kIHNlbnNvcmVuLCBzZW5kIG3DpWxpbmdlbiBtZWQgTVFUVCwgb2cgZm9ya2xhciBodm9yZGFuIGRhdGEgZW5kZXIgaSBkaXQgZGFzaGJvYXJkLiBEb2t1bWVudMOpciDDqXQgdmFsZyBvZyBhZnByw7h2IGzDuHNuaW5nZW4gc2FtbWVuLigAsvIZAg9iYWNrZ3JvdW5kQ29sb3IBdwdkZWZhdWx0KACy8hkCCXRleHRDb2xvcgF3B2RlZmF1bHQoALLyGQINdGV4dEFsaWdubWVudAF3BGxlZnQoALLyGQECaWQBdwxjYXB0dXJlLW5vdGUA';
const members = ['Anna Nielsen', 'Emil Hansen', 'Noor Ali', 'Oscar Lund', 'Freja Madsen', 'Malik Jensen']
  .map((displayName, index) => ({ id: fixtureId(100 + index), userSub: index === 0 ? U : fixtureId(200 + index),
    displayName, groupIds: [fixtureId(78 + Math.floor(index / 2))] }));
const groups = [0, 1, 2].map(index => ({ id: fixtureId(78 + index), holdId: H, name: ['Projekt Atlas', 'Projekt Nova', 'Projekt Pixel'][index],
  version: 2, groupSetId: SET, members: members.slice(index * 2, index * 2 + 2) }));
const sets = [{ id: SET, holdId: H, name: 'Projektgrupper · november', archived: false, version: 2, groupCount: 3, memberCount: 6 },
  { id: fixtureId(67), holdId: H, name: 'Makkerpar · oktober', archived: true, version: 1, groupCount: 3, memberCount: 6 }];
const quests = [{ id: Q, holdId: H, title: 'Byg et dashboard til jeres sensor',
  description: 'Vis sensordata i et dashboard, forklar datamodellen, og vedhæft kode og en kort evaluering.',
  submissionMode: 'group', published: true, deadlineUtc: '2026-11-06T12:00:00Z', canManage: true,
  kind: 'main', groupSetId: SET, required: true, audience: { mode: 'hold' }, allowedGroupIds: groups.map(group => group.id) },
  { id: fixtureId(45), holdId: H, title: 'Projektugens refleksion', description: 'Hvad lærte du i dag, og hvad er dit næste skridt?',
    submissionMode: 'individual', published: true, deadlineUtc: '2026-11-10T12:00:00Z', canManage: true,
    kind: 'daily', required: true, audience: { mode: 'hold' } }];
const file = { id: F, originalName: 'Sensor-dashboard.pdf', contentType: 'application/pdf', sizeBytes: 148320 };
const submission = { id: S, questId: Q, groupId: G, state: 'changes-requested', version: 3,
  draftText: 'Vi har samlet sensordata i et dashboard og dokumenteret datamodellen.', draftFiles: [file],
  revisions: [{ id: R, number: 1, text: 'Datamodellen adskiller sensor, måling og tidspunkt.', submittedBySub: U,
    submittedAtUtc: '2026-11-06T11:40:00Z', files: [file], participants: members.slice(0, 2).map(member => ({ membershipId: member.id, userSub: member.userSub, displayName: member.displayName })),
    feedback: [{ id: fixtureId(59), teacherSub: T, text: 'En klar datamodel. Forklar også, hvad der sker, når sensoren mister forbindelsen.',
      decision: 'changes-requested', createdAtUtc: '2026-11-07T10:15:00Z', attachments: [] }] }], canEdit: false, canReview: true };
const progress = { expected: 6, delivered: 4, missing: 2, overdue: 2, pendingReview: 2, approved: 2, changesRequested: 2, drafts: 1, ungrouped: 0 };
const overview = { holdId: H, generatedAtUtc: stamp, requiredProgress: progress, quests: [{ questId: Q,
  title: quests[0].title, kind: 'main', submissionMode: 'group', published: true, required: true, deadlineUtc: quests[0].deadlineUtc,
  groupSetId: SET, groupSetName: sets[0].name, progress, reviewTasks: 2, upcoming: false,
  members: members.map((member, index) => ({ membershipId: member.id, displayName: member.displayName,
    state: ['changes-requested', 'changes-requested', 'approved', 'approved', 'draft', 'missing'][index], overdue: index >= 4,
    submittedLate: false, ungrouped: false, submissionId: index < 4 ? S : null, groupId: groups[Math.floor(index / 2)].id,
    groupName: groups[Math.floor(index / 2)].name, submittedAtUtc: index < 4 ? '2026-11-06T11:40:00Z' : null, revisionNumber: index < 4 ? 1 : null, isCurrentRevision: index < 4 })) }] };
const calendar = [{ id: fixtureId(62), holdId: H, title: 'Workshop: MQTT og sensordata', description: 'Vi forbinder sensoren og afprøver MQTT.',
  startUtc: '2026-11-09T08:00:00Z', endUtc: '2026-11-09T11:00:00Z', location: 'Lokale 204', kind: 'workshop', version: 1 },
  { id: fixtureId(63), holdId: H, title: 'Fælles kodegennemgang', description: 'Vis løsningen, og giv konkret feedback.',
    startUtc: '2026-11-11T09:00:00Z', endUtc: '2026-11-11T11:00:00Z', location: 'Værkstedet', kind: 'teaching', version: 1 }];
const materials = [{ id: fixtureId(61), holdId: H, fileAssetId: F, title: 'Sensorens datablad og forbindelser',
  description: 'Datablad og forslag til den første afprøvning.', published: true, createdAtUtc: stamp }];
const resources = [{ resourceId: P, kind: 'page', title: 'Fra sensor til dashboard', published: true, parentPageId: null },
  { resourceId: Q, kind: 'quest', title: quests[0].title, published: true },
  { resourceId: materials[0].id, kind: 'material', title: materials[0].title, published: true, fileAssetId: F }];
const course = { id: C, holdId: H, title: 'Fra sensor til dashboard', description: 'Byg en samlet IoT-løsning fra måling til dashboard.',
  learningObjectives: 'Forbind en sensor, send målinger med MQTT, og forklar datamodellen.', startsOn: '2026-11-09',
  endsOn: '2026-11-20', published: true, archived: false, version: 2, resources };
const page = { id: P, holdId: H, title: course.title, parentPageId: null, updatedAtUtc: stamp,
  publishedAtUtc: '2026-11-06T08:00:00Z', hasUnpublishedChanges: false };
const workspace = { courseId: W, holdId: H, name: 'H3 · Programmering', description: 'Holdets undervisning og projekter',
  canManage: true, memberCount: 6, groupCount: 3, materialCount: 1, pendingReviews: 2, questCount: 2, draftQuestCount: 0,
  pages: [page], agenda: [{ id: calendar[0].id, title: calendar[0].title, startUtc: calendar[0].startUtc, kind: 'event', location: 'Lokale 204' }],
  reviews: [{ questId: Q, title: quests[0].title, count: 2 }], lectioKeys: ['H3-2026'], progress,
  courses: [{ id: C, title: course.title, description: course.description, startsOn: course.startsOn, endsOn: course.endsOn, published: true }], groupSetCount: 2 };
const student = { lectioId: 'qa-anna', name: 'Anna Nielsen', className: 'H3 · Programmering', school: 'Mercantec', status: 'I skole',
  workplace: { name: 'Eksempelvirksomhed' }, vuf: { earned: 12, required: 10, ok: true },
  h6: { level: 'H3', name: 'Programmering', startDate: '2026-11-02', endDate: '2026-12-11' },
  forloeb: { name: 'H3', startDate: '2026-11-02', endDate: '2026-12-11' },
  schoolPeriods: [{ name: 'H3', startDate: '2026-11-02', endDate: '2026-12-11' }] };
const plan = { className: student.className, schoolPeriods: [{ name: 'H2', startDate: '2026-04-06', endDate: '2026-05-15' },
  ...student.schoolPeriods, { name: 'H4', startDate: '2027-03-01', endDate: '2027-04-09' }] };
const templates = [['Dagens refleksion', 'daily'], ['Min uge i værkstedet', 'weekly'], ['En god idé at gemme', 'free']]
  .map(([title, cadence], index) => ({ id: fixtureId(300 + index), documentId: fixtureId(300 + index), title, cadence, isActive: true, version: 1, updatedAtUtc: stamp }));
const notebookPages = [{ id: fixtureId(310), notebookId: N, title: 'Fra sensor til dashboard', parentPageId: null,
  kind: 'note', tags: ['iot'], periodStart: null, updatedAtUtc: stamp, revision: 1 },
  { id: fixtureId(311), notebookId: N, title: 'Datamodel og MQTT', parentPageId: fixtureId(310), kind: 'note', tags: ['iot'], periodStart: null, updatedAtUtc: stamp, revision: 1 }];
const actionItems = [{ kind: 'review', severity: 'important', holdId: H, workspaceId: W, courseId: C,
  resourceId: Q, submissionId: S, title: 'Giv feedback på sensorprojektet', holdName: workspace.name, deadlineUtc: null, count: 2, state: null, targetId: null },
  { kind: 'poll', severity: 'optional', holdId: H, workspaceId: W, courseId: C, resourceId: fixtureId(320),
    submissionId: null, title: 'Hvordan gik arbejdet i dag?', holdName: workspace.name, deadlineUtc: null, count: 1, state: null, targetId: null }];
export const avatarProfile = Object.freeze({ selectedAvatarId: 'bip01', ownedAvatarIds: ['starter', 'bip01', 'raymond'],
  gold: 320, level: 7, ownedItemIds: ['desk', 'bed', 'chair', 'plant', 'board'], canImportLegacy: false, revision: 1,
  unlockProgress: [{ avatarId: 'santa', requirementId: 'christmas-gift-recipients', current: 2, required: 3, isMet: false }] });

function json(body, status = 200) { return { status, contentType: 'application/json', body: JSON.stringify(body) }; }
export function browserSeed({ guest = false } = {}) {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const expiry = 2524608000; // A fixture identity only; no server ever receives this token.
  return { fixedTime: FIXED_TIME, token: guest ? null : `${encode({ alg: 'none', typ: 'JWT' })}.${encode({ sub: T, name: 'Underviser QA',
    email: 'underviser@example.test', role: 'Teacher', login_method: 'isolated_fixture', exp: expiry })}.fixture-only`,
  expiresAt: String(expiry), theme: 'light', course: 'h3' };
}

/** Returns a local response, never a URL to an API. Unknown endpoints fail closed. */
export function fixtureResponse(urlLike, method = 'GET') {
  const url = new URL(urlLike, 'http://fixture.invalid');
  const p = url.pathname.replace(/^\/api\/?/, '');
  if (method !== 'GET' && method !== 'HEAD') {
    if (method === 'POST' && p === 'notebooks/mine') return json({ id: N, ownerSub: T, ownerName: 'Underviser QA', canEdit: true, canShare: true });
    if (method === 'POST' && p === 'references/graph') {
      const entities = [
        { reference: { kind: 'notebook-page', id: notebookPages[0].id }, title: notebookPages[0].title, href: `/notebook/${notebookPages[0].id}`, contextLabel: 'Notesbog' },
        { reference: { kind: 'notebook-page', id: notebookPages[1].id }, title: notebookPages[1].title, href: `/notebook/${notebookPages[1].id}`, contextLabel: 'Notesbog' },
        { reference: { kind: 'notion-page', id: P }, title: page.title, href: `/notion/${P}`, contextLabel: workspace.name, holdId: H }
      ];
      const nodes = entities.map(entity => ({ key: `${entity.reference.kind}:${entity.reference.id}`, entity }));
      return json({ nodes, edges: [{ fromKey: nodes[0].key, toKey: nodes[1].key }, { fromKey: nodes[1].key, toKey: nodes[2].key }], total: nodes.length, hasMore: false });
    }
    if (['notebooks/profile', 'gifts/profile', 'me/actions/read'].includes(p)) return json({});
    if (p === 'lectio/auth-links/resolve') return json({ links: [{ lectioId: student.lectioId, authSub: U }] });
    return json({ error: 'capture_fixture_mutation_blocked' }, 404);
  }
  if (p === 'me') return json({ sub: T, name: 'Underviser QA', email: 'underviser@example.test', roles: ['Teacher'] });
  if (p === 'me/profile') return json({ displayName: 'Underviser QA' });
  if (p === 'me/avatars') return json(avatarProfile);
  if (p === 'me/actions') return json({ generatedAtUtc: stamp, items: actionItems });
  if (p === 'me/actions/read') return json({});
  if (p === 'gifts' || p === 'gifts/inbox') return json({ received: [], sent: [], unopenedCount: 0 });
  if (p === 'gifts/recipients') return json([]);
  if (p === 'gifts/profile' || p === 'notebooks/profile') return json({});
  if (p === 'me/memberships') return json([{ forloebId: W, forloebName: workspace.name, holdId: H, holdName: workspace.name, role: 'Elev' }]);
  if (p === 'forloeb') return json([{ id: W, name: workspace.name, description: course.description }]);
  if (p === `hold-workspace/${W}` || p === `hold-workspace/holds/${H}`) return json(workspace);
  if (p === 'quests/holds') return json([{ id: H, name: workspace.name, canManage: true }]);
  if (p === 'quests/members') return json(members);
  if (p === 'quests') return json(quests);
  if (p.startsWith('quests/')) return json(quests.find(quest => quest.id === p.split('/')[1]) ?? {}, quests.some(quest => quest.id === p.split('/')[1]) ? 200 : 404);
  if (p === 'groups') return json(groups);
  if (p === 'materials') return json(materials);
  if (p === 'submissions') return json([submission]);
  if (p === `submissions/${S}`) return json(submission);
  if (p === `holds/${H}/submission-overview`) return json(overview);
  if (p === `holds/${H}/calendar`) return json(calendar);
  if (p === `holds/${H}/teaching-courses`) return json({ holdId: H, canManage: true, courses: [course] });
  if (p === `holds/${H}/teaching-courses/catalog`) return json(resources);
  if (p === `holds/${H}/group-sets`) return json(sets);
  if (p === 'notion/holds') return json([{ id: H, name: workspace.name, canManage: true }]);
  if (p === `notion/holds/${H}/members`) return json(members);
  if (p === `notion/holds/${H}/groups`) return json(groups);
  if (p === 'notion/pages') return json([page]);
  if (p === 'references/backlinks') return json([]);
  if (p === `notion/pages/${P}/context` || p === `notion/pages/${P}/published-context`) return json({ pageId: P,
    holdId: H, title: page.title, canEdit: false, members, polls: [], pages: [page], updatedAtUtc: stamp,
    publishedAtUtc: page.publishedAtUtc, publishedStateBase64: documentSnapshot, drawings: [] });
  if (p === `notion/holds/${H}/graph` || p === `notion/graph/${P}`) return json({ holdId: H, collectionId: P,
    title: workspace.name, canRename: true, wholeHold: true, nodes: [{ id: P, title: course.title }, { id: fixtureId(79), title: 'MQTT og sensordata' },
      { id: fixtureId(80), title: 'Datamodellen' }], edges: [{ fromId: P, toId: fixtureId(79) }, { fromId: P, toId: fixtureId(80) }] });
  if (p === 'notebooks/today') return json('2026-11-09');
  if (p === 'notebooks/templates') return json(templates);
  if (p === 'notebooks/mine') return json({ id: N, ownerSub: T, ownerName: 'Underviser QA', canEdit: true, canShare: true });
  if (p === 'notebooks/shared') return json([]);
  if (p === 'notebooks/teacher') return json(members.slice(0, 4).map((member, index) => ({ id: fixtureId(400 + index), ownerSub: member.userSub,
    ownerName: member.displayName, holds: [workspace.name], latestJournalDate: '2026-11-09', updatedAtUtc: stamp })));
  if (p === `notebooks/${N}/pages`) return json({ items: notebookPages, total: notebookPages.length });
  if (p === `notebooks/${N}/shares`) return json([]);
  if (p.includes('notebooks') && p.endsWith('/graph')) return json({ notebookId: N, nodes: notebookPages,
    total: notebookPages.length, edges: [{ fromId: notebookPages[0].id, toId: notebookPages[1].id }] });
  if (p.startsWith('notebooks/pages/') && p.endsWith('/context')) return json({ documentId: p.split('/')[2], pageId: p.split('/')[2], notebookId: N,
    title: notebookPages.find(entry => entry.id === p.split('/')[2])?.title ?? course.title, kind: 'note', canRead: true, canEdit: false,
    canComment: true, canShare: false, stateBase64: documentSnapshot, updatedAtUtc: stamp, revision: 1, parentPageId: null, pages: notebookPages, tags: ['iot'], drawings: [] });
  if (p.startsWith('notebooks/pages/') && (p.endsWith('/comments') || p.endsWith('/backlinks'))) return json([]);
  if (p === 'lectio/me') return json({ lectio: { lectioId: student.lectioId } });
  if (p === 'lectio/me/student') return json(student);
  if (p === 'lectio/programmeringselever') return json({ students: [student, { ...student, lectioId: 'qa-emil', name: 'Emil Hansen', vuf: { earned: 8, required: 10, ok: false } }] });
  if (p === 'lectio/auth-links/resolve') return json({ links: [{ lectioId: student.lectioId, authSub: U }] });
  if (p.startsWith('lectio/students/') && p.endsWith('/studieplan')) return json(plan);
  if (p === 'users') return json([{ id: U, sub: U, displayName: 'Anna Nielsen', name: 'Anna Nielsen', email: 'anna@example.test', appRole: 'elev', roles: ['Elev'] }]);
  if (p === 'lectio/holds/tracked' || p === 'lectio/holds/saved') return json({ summary: { holdCount: 1, memberCount: 6, uniqueStudents: 6 },
    holds: [{ id: 'qa-h3', lectioHoldKey: 'H3-2026', name: workspace.name, year: '2026', yearLabel: '2026/2027', memberCount: 6,
      startDate: '2026-11-02', endDate: '2026-12-11', dateSource: 'Lectio', isTracked: true }] });
  if (p === 'lectio/hold' || p === 'lectio/holds') return json({ holds: [] });
  if (p === 'calendar/mine') return json({ azureUpn: 'underviser@example.test', fromUtc: url.searchParams.get('from'), toUtc: url.searchParams.get('to'), configured: true,
    events: calendar.map((entry, index) => ({ id: `fixture-outlook-${index}`, subject: entry.title, start: entry.startUtc, end: entry.endUtc,
      isAllDay: false, location: entry.location, isOnlineMeeting: false, isPrivate: false, isCancelled: false, showAs: 'busy' })) });
  if (p === 'emojis') return json([{ id: fixtureId(500), name: 'vatson', contentType: 'image/webp', sizeBytes: 1000, updatedAtUtc: stamp,
    contentUrl: '/images/characters/vatson/vatson.webp', category: 'people', keywords: ['lup'], altText: 'Vatson – testemoji' }]);
  if (p === 'platform-updates') return json([{ id: 'fixture-update', date: '2026-11-09', title: 'Eksempel på en visuel opdatering',
    description: 'Denne fiktive post viser ændringsoversigtens layout.', after: { id: 'fixture-vatson', alt: 'Vatson som testbillede' }, before: null }]);
  if (p === 'platform-updates/images/fixture-vatson') return { status: 200, contentType: 'image/webp', staticPath: '/images/characters/vatson/vatson.webp' };
  return json({ error: 'capture_fixture_route_missing' }, 404);
}
