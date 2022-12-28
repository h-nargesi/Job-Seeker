create table Trend (
	TrendID			integer 	not null	primary key,
	AgencyID		integer		not null,
	LastActivity	timestamp	not null	default current_timestamp,
	Type			text		not null,
	State			text		not null,
	
	unique			(AgencyID, Type),
	foreign key		(AgencyID) references Agency (AgencyID) on delete no action
)