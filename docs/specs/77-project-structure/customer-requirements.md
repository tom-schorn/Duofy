# Customer requirements: Project structure

Format: 2

## Background

The .NET rebuild grew to 14 projects lying side by side. In the check-in of 2026-10-26 the human, who knows Python and React from the original Duofy, asked why the backend consists of several projects when it is one backend, and why the data formats are called "Contracts" although Duofy also has contracts (Verträge). Only the API uses the data layer and the domain rules; app and web read the backend only through the API and the shared data formats. The human decided: one backend project, the data formats renamed to Schemas, and folders per area.

## Goal

The repository reads like the original: one backend, one place for the shared data formats, the shared pages, the web host, the Android app and the tests, each in its own folder.

## Users

The human and the team, whenever they open the solution.

## Requirements

R1 (must): Duofy.Api, Duofy.Data and Duofy.Domain become one project Duofy.Backend with the subfolders Api, Data and Domain — because it is one backend, and only the API used the other two.
R2 (must): Duofy.Contracts is renamed Duofy.Schemas, project and namespace — because "Contracts" is mistaken for the feature Verträge.
R3 (must): The projects lie in the folders Backend, Shared, Web, Mobile and Tests, and the solution shows the same folders — because the areas should be visible at a glance.
R4 (must): Behaviour, endpoints, database tables and migration history do not change; every test that passed before passes afterwards — because this is a move, not a rewrite.
R5 (must): The tests of the former Api and Domain projects live in one test project Duofy.Backend.Tests — because their project is one now.
R6 (wont): Renaming namespaces of Api, Data and Domain, changing the shared pages, the API client or the app — because it is not needed for the goal.

## Constraints

- The migrations keep their ids; EF Core finds them in the new assembly.
- README, the check command and the specs' tracing tables point to the new paths.

## Open questions

None.
