import test from 'node:test';
import assert from 'node:assert/strict';
import { splitSlides, selectedMembers, pollUrl, pageUrl, studentUrl, pollCreateUrl } from '../src/document-utils.mjs';

test('presentation removes class rosters including nested rosters without changing source', () => {
  const blocks = [{ type: 'paragraph', content: 'Introduktion', children: [{ type: 'classRoster', props: { membershipIds: 'secret' } }] }, { type: 'slideBreak' }, { type: 'classRoster' }, { type: 'poll', props: { pollId: 'a' } }];
  const slides = splitSlides(blocks);
  assert.equal(slides.length, 2);
  assert.deepEqual(slides[0][0].children, []);
  assert.deepEqual(slides[1], [blocks[3]]);
  assert.equal(blocks[0].children.length, 1);
});

test('an empty document stays navigable while blank slides are omitted', () => {
  assert.equal(splitSlides([]).length, 1);
  assert.equal(splitSlides([{type:'slideBreak'}]).length, 1);
  assert.deepEqual(splitSlides([{type:'classRoster'}, {type:'paragraph',content:[]}, {type:'slideBreak'}, {type:'heading',content:'Dagens øvelse'}]), [[{type:'heading',content:'Dagens øvelse'}]]);
});

test('roster choices resolve by stable membership ID against current authorized roster', () => {
  const roster = [{ id: 'one', displayName: 'Samme navn' }, { id: 'two', displayName: 'Samme navn' }];
  assert.deepEqual(selectedMembers(roster, 'two,unknown'), [roster[1]]);
  assert.deepEqual(selectedMembers(roster, ''), roster);
});

test('poll links only accept UUIDs, including presentation links', () => {
  const id = 'ba72e62b-e57e-43af-b655-f8cdd2aeacbe';
  assert.equal(pollUrl(id), '/polls/' + id);
  assert.equal(pollUrl(id, true), '/teacher/polls/' + id + '/present');
  assert.equal(pollUrl('javascript:alert(1)'), null);
});

test('teaching links preserve only typed workspace, course and hold context', () => {
  const page = 'ba72e62b-e57e-43af-b655-f8cdd2aeacbe';
  const context = {
    workspaceId: '11111111-1111-4111-8111-111111111111',
    courseId: '22222222-2222-4222-8222-222222222222',
    holdId: '33333333-3333-4333-8333-333333333333',
    returnUrl: 'https://untrusted.example/'
  };
  const query = '?workspace=' + context.workspaceId + '&course=' + context.courseId + '&hold=' + context.holdId;
  assert.equal(pageUrl(page, true, context), '/notion/' + page + query + '&published=true');
  assert.equal(pollUrl(page, true, context), '/teacher/polls/' + page + '/present' + query);
  assert.equal(pollCreateUrl(context), '/teacher/polls/new' + query);
  assert.equal(pollCreateUrl({workspaceId:'https://untrusted.example/'}), '/teacher/polls/new');
  assert.equal(studentUrl(context.holdId, page, context), '/notion/holds/' + context.holdId + '/students/' + page + query);
  assert.equal(pageUrl(page, false, {workspaceId:'https://untrusted.example/',courseId:context.courseId}), '/notion/' + page);
  assert.equal(pollUrl(page, false, {holdId: context.holdId, courseId:context.courseId}), '/polls/' + page + '?course=' + context.courseId + '&hold=' + context.holdId);
});
