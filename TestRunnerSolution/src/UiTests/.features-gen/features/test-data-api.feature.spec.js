// Generated from: features/test-data-api.feature
import { test } from "playwright-bdd";

test.describe('Consume test data through the API', () => {

  test('Get available test data tables', { tag: ['@database'] }, async ({ When, Then, request }) => { 
    await When('I request the list of test data tables', null, { request }); 
    await Then('the test data tables response should contain at least one table'); 
  });

  test('Get rows from an available table', { tag: ['@database'] }, async ({ Given, When, Then, request }) => { 
    await Given('I know an available test data table', null, { request }); 
    await When('I request test data for that table', null, { request }); 
    await Then('the test data rows response should be successful'); 
  });

  test('Get test plan through API', { tag: ['@database'] }, async ({ Given, When, Then, request }) => { 
    await Given('the test data API is available', null, { request }); 
    await When('I request rows from table "test_plans"', null, { request }); 
    await Then('the response matches the test plan schema'); 
  });

  test('Test plan rows visibly fail against an incompatible schema', { tag: ['@database', '@intentionalfail'] }, async ({ Given, When, Then, request }) => { 
    await Given('the test data API is available', null, { request }); 
    await When('I request rows from table "test_plans"', null, { request }); 
    await Then('the response incorrectly matches the incompatible schema'); 
  });

  test('Get a single bug row through API', { tag: ['@database'] }, async ({ Given, When, Then, request }) => { 
    await Given('the test data API is available', null, { request }); 
    await When('I request 1 row from table "bugs"', null, { request }); 
    await Then('the response matches the bug schema'); 
  });

  test('Bug row visibly fails against an incompatible schema', { tag: ['@database', '@intentionalfail'] }, async ({ Given, When, Then, request }) => { 
    await Given('the test data API is available', null, { request }); 
    await When('I request 1 row from table "bugs"', null, { request }); 
    await Then('the bug response incorrectly matches the incompatible schema'); 
  });

});

// == technical section ==

test.use({
  $test: [({}, use) => use(test), { scope: 'test', box: true }],
  $uri: [({}, use) => use('features/test-data-api.feature'), { scope: 'test', box: true }],
  $bddFileData: [({}, use) => use(bddFileData), { scope: "test", box: true }],
});

const bddFileData = [ // bdd-data-start
  {"pwTestLine":6,"pickleLine":4,"tags":["@database"],"steps":[{"pwStepLine":7,"gherkinStepLine":5,"keywordType":"Action","textWithKeyword":"When I request the list of test data tables","stepMatchArguments":[]},{"pwStepLine":8,"gherkinStepLine":6,"keywordType":"Outcome","textWithKeyword":"Then the test data tables response should contain at least one table","stepMatchArguments":[]}]},
  {"pwTestLine":11,"pickleLine":8,"tags":["@database"],"steps":[{"pwStepLine":12,"gherkinStepLine":9,"keywordType":"Context","textWithKeyword":"Given I know an available test data table","stepMatchArguments":[]},{"pwStepLine":13,"gherkinStepLine":10,"keywordType":"Action","textWithKeyword":"When I request test data for that table","stepMatchArguments":[]},{"pwStepLine":14,"gherkinStepLine":11,"keywordType":"Outcome","textWithKeyword":"Then the test data rows response should be successful","stepMatchArguments":[]}]},
  {"pwTestLine":17,"pickleLine":13,"tags":["@database"],"steps":[{"pwStepLine":18,"gherkinStepLine":14,"keywordType":"Context","textWithKeyword":"Given the test data API is available","stepMatchArguments":[]},{"pwStepLine":19,"gherkinStepLine":15,"keywordType":"Action","textWithKeyword":"When I request rows from table \"test_plans\"","stepMatchArguments":[{"group":{"start":26,"value":"\"test_plans\"","children":[{"start":27,"value":"test_plans","children":[{}]},{"children":[{}]}]},"parameterTypeName":"string"}]},{"pwStepLine":20,"gherkinStepLine":16,"keywordType":"Outcome","textWithKeyword":"Then the response matches the test plan schema","stepMatchArguments":[]}]},
  {"pwTestLine":23,"pickleLine":25,"tags":["@database","@intentionalfail"],"steps":[{"pwStepLine":24,"gherkinStepLine":26,"keywordType":"Context","textWithKeyword":"Given the test data API is available","stepMatchArguments":[]},{"pwStepLine":25,"gherkinStepLine":27,"keywordType":"Action","textWithKeyword":"When I request rows from table \"test_plans\"","stepMatchArguments":[{"group":{"start":26,"value":"\"test_plans\"","children":[{"start":27,"value":"test_plans","children":[{}]},{"children":[{}]}]},"parameterTypeName":"string"}]},{"pwStepLine":26,"gherkinStepLine":28,"keywordType":"Outcome","textWithKeyword":"Then the response incorrectly matches the incompatible schema","stepMatchArguments":[]}]},
  {"pwTestLine":29,"pickleLine":30,"tags":["@database"],"steps":[{"pwStepLine":30,"gherkinStepLine":31,"keywordType":"Context","textWithKeyword":"Given the test data API is available","stepMatchArguments":[]},{"pwStepLine":31,"gherkinStepLine":32,"keywordType":"Action","textWithKeyword":"When I request 1 row from table \"bugs\"","stepMatchArguments":[{"group":{"start":10,"value":"1"},"parameterTypeName":"int"},{"group":{"start":27,"value":"\"bugs\"","children":[{"start":28,"value":"bugs","children":[{}]},{"children":[{}]}]},"parameterTypeName":"string"}]},{"pwStepLine":32,"gherkinStepLine":33,"keywordType":"Outcome","textWithKeyword":"Then the response matches the bug schema","stepMatchArguments":[]}]},
  {"pwTestLine":35,"pickleLine":42,"tags":["@database","@intentionalfail"],"steps":[{"pwStepLine":36,"gherkinStepLine":43,"keywordType":"Context","textWithKeyword":"Given the test data API is available","stepMatchArguments":[]},{"pwStepLine":37,"gherkinStepLine":44,"keywordType":"Action","textWithKeyword":"When I request 1 row from table \"bugs\"","stepMatchArguments":[{"group":{"start":10,"value":"1"},"parameterTypeName":"int"},{"group":{"start":27,"value":"\"bugs\"","children":[{"start":28,"value":"bugs","children":[{}]},{"children":[{}]}]},"parameterTypeName":"string"}]},{"pwStepLine":38,"gherkinStepLine":45,"keywordType":"Outcome","textWithKeyword":"Then the bug response incorrectly matches the incompatible schema","stepMatchArguments":[]}]},
]; // bdd-data-end