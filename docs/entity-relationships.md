# Entity Relationships - Drafts Application

> The Drafts app has multiple players and games. Players can join groups and communicate with other players in their groups in The Lovbby and in-game Chat. Players can initiate a game with other players in their group/s who can join and play it.

## Core Entities

### Application Users
A Drafts application user represents a person who can play games and participate in groups. Each user has a unique name, assigned roles, and authentication credentials.

### Groups
A group is a collection of users who can communicate with each other and play games together. Each group has a name, description, and designated owner.

### Group Memberships
A group membership represents the relationship between a user and a group, indicating when the user joined the group.

### Application Settings
Application settings contain global configuration values that control game behavior, timeouts, and system preferences.

## Entity Relationships

### User to Groups
A user can belong to zero or more groups. Users who are not in any group cannot access group chat features. Users who belong to groups can only communicate with members of their own groups.

### Group to Users
A group has one designated owner who is also a member of the group. A group can have multiple members, including the owner. All members of a group can communicate with each other.

### Group Ownership
Each group has exactly one owner who is a user. The owner has special privileges for managing the group. The owner is automatically considered a member of their own group.

### Membership Tracking
When a user joins a group, a membership record is created with the join timestamp. This allows tracking of when users became members of specific groups.

## User Roles

### Admin Users
Admin users have full system access and can perform administrative functions. Admin users can access all groups and system features regardless of group membership.

### Player Users
Player users have standard access limited to their group memberships. Players can only see and interact with games and chat within their assigned groups.

## Communication Boundaries

#### Lobby Chat
Lobby chat is the main communication system where users can send text messages before and after games. Lobby chat messages are filtered to only show messages from users within the same group. Users in different groups cannot see each other's lobby chat messages.

#### In-Game Chat
In-game chat allows players to communicate during active games. In-game chat is limited to players who are currently participating in the same game session.

#### In-Game Voice
In-game voice communication allows real-time audio chat between players during active games. Voice chat is also available to players in the same game session and provides immediate verbal communication.

### Admin Override
Admin users can bypass group restrictions and access all groups and system features. Admin users can see all lobby chat messages and participate in any game.

### Isolated Users
Users who are not members of any group cannot play any games or send lobby messages. Isolated users are completely restricted from game participation and lobby communication.

## Game Context

### Game Creation
Games can be created by any user and may be associated with specific groups for access control.

### Game Access
Players can only join games that are accessible to their groups. Admin users can join any game regardless of group association. _Only 2 players can be in a game, the intiator and the player who joins._

### Game Visibility
The list of available games is filtered based on the user's group memberships to show only relevant games. _A game that has be joined should not be visible to other players._

## Data Flow

### User Authentication
Users authenticate with their name and PIN credentials. Successful authentication grants access based on their roles and group memberships.

### Group Assignment
Users are assigned to groups through membership records. _Currently only Admin can create players and assign them to groups._  The group owner relationship is stored separately from regular membership.

### Permission Evaluation
When users attempt actions, the system evaluates their permissions based on their roles, group memberships, and ownership relationships.

## Entity Summary

### Users/Players Table
Stores user/player account information including names, roles, and authentication credentials. Primary key identifies each unique user.

### Groups Table
Stores group definitions including names, descriptions, and owner references. Foreign key links to the user who owns the group.

### GroupMembers Table
Stores many-to-many relationships between users and groups. Composite primary key ensures a user can only be a member of a specific group once. Tracks join timestamps.

### Settings Table
Stores application-wide configuration values. Single record contains all system settings.

## Relationship Constraints

### Foreign Key Constraints
Group owner must be a valid user. Group members must be valid users. Groups must exist before members can be assigned.

### Unique Constraints
Group names must be unique. User names must be unique. User-group combinations must be unique.

### Cascade Behavior
Deleting a user removes their group memberships. Deleting a group removes all associated memberships. Group owner references are protected to prevent orphaned groups.

## Access Patterns

### User-Centric Access
From a user perspective, the system shows their groups, group members, and accessible games based on their memberships.

### Group-Centric Access
From a group perspective, the system shows all members, the owner, and group-related activities.

### Admin Access
From an admin perspective, the system provides visibility into all users, groups, and system-wide settings.
