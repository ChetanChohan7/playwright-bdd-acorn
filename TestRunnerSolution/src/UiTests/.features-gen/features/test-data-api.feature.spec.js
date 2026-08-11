// Generated from: features/test-data-api.feature
import { test } from "playwright-bdd";

test.describe('Consume test data through the API', () => {

  test('Get available test data tables', { tag: ['@database'] }, async ({ When, Then, request }) => { 
    await When('I request the list of test data tables', null, { request }); 
    await Then('the test data tables response should contain at least one table'); 
  });

  test('Get rows from an available table', { tag: ['@database'] }, async ({ Given, When, Then, And, request }) => { 
    await Given('I know an available test data table', null, { request }); 
    await When('I request test data for that table', null, { request }); 
    await Then('the response status should be 200'); 
    await And('the test data rows response should be successful'); 
    await And('the API response should match the database'); 
  });

  test('Get test plan rows through API', { tag: ['@database'] }, async ({ Given, When, Then, And, request }) => { 
    await Given('the test data API is available', null, { request }); 
    await When('I request rows from table "test_plans"', null, { request }); 
    await Then('the response status should be 200'); 
    await And('the response matches the test plan schema'); 
    await And('the API response should match the database'); 
  });

  test('Test plan rows visibly fail against an incompatible schema', { tag: ['@database', '@intentionalfail'] }, async ({ Given, When, Then, And, request }) => { 
    await Given('the test data API is available', null, { request }); 
    await When('I request rows from table "test_plans"', null, { request }); 
    await Then('the response status should be 200'); 
    await And('the response incorrectly matches the incompatible schema'); 
  });

  test('Get a single bug row through API', { tag: ['@database'] }, async ({ Given, When, Then, And, request }) => { 
    await Given('the test data API is available', null, { request }); 
    await When('I request 1 rows from table "bugs"', null, { request }); 
    await Then('the response status should be 200'); 
    await And('the response matches the bug schema'); 
    await And('the API response should match the database'); 
  });

  test('Bug row visibly fails against an incompatible schema', { tag: ['@database', '@intentionalfail'] }, async ({ Given, When, Then, And, request }) => { 
    await Given('the test data API is available', null, { request }); 
    await When('I request 1 row from table "bugs"', null, { request }); 
    await Then('the response status should be 200'); 
    await And('the bug response incorrectly matches the incompatible schema'); 
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
  {"pwTestLine":11,"pickleLine":8,"tags":["@database"],"steps":[{"pwStepLine":12,"gherkinStepLine":9,"keywordType":"Context","textWithKeyword":"Given I know an available test data table","stepMatchArguments":[]},{"pwStepLine":13,"gherkinStepLine":10,"keywordType":"Action","textWithKeyword":"When I request test data for that table","stepMatchArguments":[]},{"pwStepLine":14,"gherkinStepLine":11,"keywordType":"Outcome","textWithKeyword":"Then the response status should be 200","stepMatchArguments":[{"group":{"start":30,"value":"200"},"parameterTypeName":"int"}]},{"pwStepLine":15,"gherkinStepLine":12,"keywordType":"Outcome","textWithKeyword":"And the test data rows response should be successful","stepMatchArguments":[]},{"pwStepLine":16,"gherkinStepLine":13,"keywordType":"Outcome","textWithKeyword":"And the API response should match the database","stepMatchArguments":[]}]},
  {"pwTestLine":19,"pickleLine":15,"tags":["@database"],"steps":[{"pwStepLine":20,"gherkinStepLine":16,"keywordType":"Context","textWithKeyword":"Given the test data API is available","stepMatchArguments":[]},{"pwStepLine":21,"gherkinStepLine":17,"keywordType":"Action","textWithKeyword":"When I request rows from table \"test_plans\"","stepMatchArguments":[{"group":{"start":26,"value":"\"test_plans\"","children":[{"start":27,"value":"test_plans","children":[{}]},{"children":[{}]}]},"parameterTypeName":"string"}]},{"pwStepLine":22,"gherkinStepLine":18,"keywordType":"Outcome","textWithKeyword":"Then the response status should be 200","stepMatchArguments":[{"group":{"start":30,"value":"200"},"parameterTypeName":"int"}]},{"pwStepLine":23,"gherkinStepLine":19,"keywordType":"Outcome","textWithKeyword":"And the response matches the test plan schema","stepMatchArguments":[]},{"pwStepLine":24,"gherkinStepLine":20,"keywordType":"Outcome","textWithKeyword":"And the API response should match the database","stepMatchArguments":[]}]},
  {"pwTestLine":27,"pickleLine":30,"tags":["@database","@intentionalfail"],"steps":[{"pwStepLine":28,"gherkinStepLine":31,"keywordType":"Context","textWithKeyword":"Given the test data API is available","stepMatchArguments":[]},{"pwStepLine":29,"gherkinStepLine":32,"keywordType":"Action","textWithKeyword":"When I request rows from table \"test_plans\"","stepMatchArguments":[{"group":{"start":26,"value":"\"test_plans\"","children":[{"start":27,"value":"test_plans","children":[{}]},{"children":[{}]}]},"parameterTypeName":"string"}]},{"pwStepLine":30,"gherkinStepLine":33,"keywordType":"Outcome","textWithKeyword":"Then the response status should be 200","stepMatchArguments":[{"group":{"start":30,"value":"200"},"parameterTypeName":"int"}]},{"pwStepLine":31,"gherkinStepLine":34,"keywordType":"Outcome","textWithKeyword":"And the response incorrectly matches the incompatible schema","stepMatchArguments":[]}]},
  {"pwTestLine":34,"pickleLine":36,"tags":["@database"],"steps":[{"pwStepLine":35,"gherkinStepLine":37,"keywordType":"Context","textWithKeyword":"Given the test data API is available","stepMatchArguments":[]},{"pwStepLine":36,"gherkinStepLine":38,"keywordType":"Action","textWithKeyword":"When I request 1 rows from table \"bugs\"","stepMatchArguments":[{"group":{"start":10,"value":"1"},"parameterTypeName":"int"},{"group":{"start":28,"value":"\"bugs\"","children":[{"start":29,"value":"bugs","children":[{}]},{"children":[{}]}]},"parameterTypeName":"string"}]},{"pwStepLine":37,"gherkinStepLine":39,"keywordType":"Outcome","textWithKeyword":"Then the response status should be 200","stepMatchArguments":[{"group":{"start":30,"value":"200"},"parameterTypeName":"int"}]},{"pwStepLine":38,"gherkinStepLine":40,"keywordType":"Outcome","textWithKeyword":"And the response matches the bug schema","stepMatchArguments":[]},{"pwStepLine":39,"gherkinStepLine":41,"keywordType":"Outcome","textWithKeyword":"And the API response should match the database","stepMatchArguments":[]}]},
  {"pwTestLine":42,"pickleLine":52,"tags":["@database","@intentionalfail"],"steps":[{"pwStepLine":43,"gherkinStepLine":53,"keywordType":"Context","textWithKeyword":"Given the test data API is available","stepMatchArguments":[]},{"pwStepLine":44,"gherkinStepLine":54,"keywordType":"Action","textWithKeyword":"When I request 1 row from table \"bugs\"","stepMatchArguments":[{"group":{"start":10,"value":"1"},"parameterTypeName":"int"},{"group":{"start":27,"value":"\"bugs\"","children":[{"start":28,"value":"bugs","children":[{}]},{"children":[{}]}]},"parameterTypeName":"string"}]},{"pwStepLine":45,"gherkinStepLine":55,"keywordType":"Outcome","textWithKeyword":"Then the response status should be 200","stepMatchArguments":[{"group":{"start":30,"value":"200"},"parameterTypeName":"int"}]},{"pwStepLine":46,"gherkinStepLine":56,"keywordType":"Outcome","textWithKeyword":"And the bug response incorrectly matches the incompatible schema","stepMatchArguments":[]}]},
]; // bdd-data-end